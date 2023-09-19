using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Silky.Core;
using Silky.Core.Exceptions;
using Silky.Core.Extensions;
using Silky.Core.Runtime.Rpc;
using Silky.Rpc.Runtime;
using Silky.Rpc.Runtime.Client;
using Silky.Rpc.Transport.Messages;

namespace Silky.Rpc.Transport
{
    public class DefaultTransportClient : ITransportClient, IDisposable
    {
        private ConcurrentDictionary<string, TaskCompletionSource<TransportMessage>> m_resultDictionary = new();

        public ILogger<DefaultTransportClient> Logger { get; set; }

        public IClientMessageSender MessageSender { get; set; }
        private readonly IMessageListener _messageListener;

        public DefaultTransportClient(IClientMessageSender messageSender, IMessageListener messageListener)
        {
            MessageSender = messageSender;
            _messageListener = messageListener;
            _messageListener.Received += MessageListenerOnReceived;
            Logger = EngineContext.Current.Resolve<ILogger<DefaultTransportClient>>() ??
                     NullLogger<DefaultTransportClient>.Instance;
        }

        private async Task MessageListenerOnReceived(IMessageSender sender, TransportMessage message)
        {
            if (!m_resultDictionary.TryGetValue(message.Id, out var task))
                return;
            task.SetResult(message);
        }
        
        public virtual async Task<RemoteResultMessage> SendAsync(RemoteInvokeMessage message, string messageId,
            int timeout = Timeout.Infinite)
        {
            var transportMessage = new TransportMessage(message, messageId);
            transportMessage.SetRpcMessageId();
            message.Attachments = RpcContext.Context.GetInvokeAttachments();
            var callbackTask =
                RegisterResultCallbackAsync(transportMessage.Id, message.ServiceEntryId, timeout);
            Logger.LogDebug(
                "Preparing to send RPC message{0}" +
                "messageId:[{1}],serviceEntryId:[{2}]", Environment.NewLine, transportMessage.Id,
                message.ServiceEntryId);

            await MessageSender.SendMessageAsync(transportMessage);
            return await callbackTask;
        }

        private async Task<RemoteResultMessage> RegisterResultCallbackAsync(string id, string serviceEntryId,
            int timeout = Timeout.Infinite)
        {
            var tcs = new TaskCompletionSource<TransportMessage>();

            m_resultDictionary.TryAdd(id, tcs);
            RemoteResultMessage remoteResultMessage = null;
            try
            {
                var resultMessage = await tcs.WaitAsync(timeout);
                remoteResultMessage = resultMessage.GetContent<RemoteResultMessage>();
                Logger.LogDebug(
                    "Received the message returned from server{0}messageId:[{1}],serviceEntryId:[{2}]",
                    Environment.NewLine, id, serviceEntryId);
                CheckRemoteResultMessage(remoteResultMessage);
                return remoteResultMessage;
            }
            finally
            {
                RpcContext.Context.SetResultAttachments(remoteResultMessage?.Attachments);
                m_resultDictionary.TryRemove(id, out tcs);
            }
        }

        private void CheckRemoteResultMessage(RemoteResultMessage remoteResultMessage)
        {
            if (remoteResultMessage.StatusCode == StatusCode.Success)
            {
                return;
            }

            if (remoteResultMessage.StatusCode == StatusCode.ValidateError)
            {
                var validateException = new ValidationException(remoteResultMessage.ExceptionMessage,
                    remoteResultMessage.ValidateErrors);


                throw validateException;
            }

            throw new SilkyException(remoteResultMessage.ExceptionMessage, remoteResultMessage.StatusCode,
                remoteResultMessage.Status);
        }

        public void Dispose()
        {
            if (MessageSender is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}