using Silky.Core.DependencyInjection;
using Silky.Core.Rpc;
using Silky.Rpc.Transport;

namespace Silky.Rpc.Security
{
    public class CurrentRpcToken : ICurrentRpcToken, IScopedDependency
    {
        public string Token { get; } = RpcContext.Context.GetAttachment(AttachmentKeys.RpcToken)?.ToString();
    }
}