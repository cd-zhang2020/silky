using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Silky.Caching;
using Silky.Core;
using Silky.Core.Extensions;
using Silky.Core.Threading;
using Silky.Rpc.Configuration;
using Silky.Rpc.Endpoint;
using Silky.Rpc.Runtime.Client;
using Silky.Rpc.Runtime.Server;

namespace Silky.Rpc.Monitor.Provider;

public class DefaultMonitorProvider : IMonitorProvider, IAsyncDisposable
{
    private readonly IDistributedCache<ClientInvokeInfo> _clientInvokeDistributedCache;
    private readonly IDistributedCache<ServerInstanceInvokeInfo> _serverInstanceInvokeInfoDistributedCache;
    private readonly ConcurrentDictionary<string, ClientInvokeInfo> _clientInvokeCache = new();


    private readonly IDistributedCache<ServerHandleInfo> _serverHandleDistributedCache;
    private readonly IDistributedCache<ServerInstanceHandleInfo> _serverInstanceHandleInfoDistributedCache;

    private readonly ConcurrentDictionary<string, ServerHandleInfo> _serverHandleInvokeCache = new();

    public ServerInstanceHandleInfo InstanceHandleInfo { get; private set; }

    public ServerInstanceInvokeInfo InstanceInvokeInfo { get; private set; }

    private readonly RpcOptions _rpcOptions;
    private readonly Timer _timer;

    public DefaultMonitorProvider(IDistributedCache<ClientInvokeInfo> clientInvokeDistributedCache,
        IDistributedCache<ServerHandleInfo> serverHandleDistributedCache,
        IDistributedCache<ServerInstanceInvokeInfo> serverInstanceInvokeInfoDistributedCache,
        IDistributedCache<ServerInstanceHandleInfo> serverInstanceHandleInfoDistributedCache,
        IOptionsMonitor<GovernanceOptions> governanceOptions,
        IOptions<RpcOptions> rpcOptions)
    {
        _clientInvokeDistributedCache = clientInvokeDistributedCache;
        _serverHandleDistributedCache = serverHandleDistributedCache;
        _serverInstanceInvokeInfoDistributedCache = serverInstanceInvokeInfoDistributedCache;
        _serverInstanceHandleInfoDistributedCache = serverInstanceHandleInfoDistributedCache;
        _rpcOptions = rpcOptions.Value;
        AsyncHelper.RunSync(async () => await InitThisInstanceInfo());

        InstanceHandleInfo.AllowMaxConcurrentCount = governanceOptions.CurrentValue.MaxConcurrentHandlingCount;
        governanceOptions.OnChange(options =>
        {
            InstanceHandleInfo.AllowMaxConcurrentCount = options.MaxConcurrentHandlingCount;
        });

        _timer = new Timer(CollectMonitorCallBack, null,
            TimeSpan.FromSeconds(_rpcOptions.CollectMonitorInfoIntervalSeconds),
            TimeSpan.FromSeconds(_rpcOptions.CollectMonitorInfoIntervalSeconds));
    }

    private async Task InitThisInstanceInfo()
    {
        var localAddress = GetLocalAddress();
        InstanceInvokeInfo =
            await _serverInstanceInvokeInfoDistributedCache.GetAsync($"InstanceInvokeInfo:{localAddress}");
        if (InstanceInvokeInfo == null)
        {
            InstanceInvokeInfo = new();
            await _serverInstanceInvokeInfoDistributedCache.SetAsync($"InstanceInvokeInfo:{localAddress}",
                InstanceInvokeInfo);
        }

        InstanceHandleInfo =
            await _serverInstanceHandleInfoDistributedCache.GetAsync($"InstanceHandleInfo:{localAddress}");
        if (InstanceHandleInfo == null)
        {
            InstanceHandleInfo = new();
            await _serverInstanceHandleInfoDistributedCache.SetAsync($"InstanceHandleInfo:{localAddress}",
                InstanceHandleInfo);
        }
    }

    private void CollectMonitorCallBack(object? state)
    {
        foreach (var invokeInfo in _clientInvokeCache)
        {
            _clientInvokeDistributedCache.Set(invokeInfo.Key, invokeInfo.Value);
        }

        foreach (var serverHandleInfo in _serverHandleInvokeCache)
        {
            _serverHandleDistributedCache.Set(serverHandleInfo.Key, serverHandleInfo.Value);
        }

        var localAddress = GetLocalAddress();
        _serverInstanceInvokeInfoDistributedCache.Set($"InstanceInvokeInfo:{localAddress}",
            InstanceInvokeInfo);
        _serverInstanceHandleInfoDistributedCache.Set($"InstanceHandleInfo:{localAddress}",
            InstanceHandleInfo);
    }

    public async Task<ServerInstanceInvokeInfo> GetInstanceInvokeInfo()
    {
        var localAddress = GetLocalAddress();
        var instanceInvokeInfo =
            await _serverInstanceInvokeInfoDistributedCache.GetAsync($"InstanceInvokeInfo:{localAddress}") ?? new();
        return instanceInvokeInfo;
    }

    public async Task<ServerInstanceHandleInfo> GetInstanceHandleInfo()
    {
        var localAddress = GetLocalAddress();
        var instanceHandleInfo =
            await _serverInstanceHandleInfoDistributedCache.GetAsync($"InstanceHandleInfo:{localAddress}") ?? new();
        return instanceHandleInfo;
    }

    public void SetClientInvokeInfo(string cacheKey, ClientInvokeInfo clientInvokeInfo)
    {
        _clientInvokeCache.AddOrUpdate(cacheKey, clientInvokeInfo, (k, v) => clientInvokeInfo);
    }

    public async Task<IReadOnlyCollection<ClientInvokeInfo>> GetServiceEntryInvokeInfos()
    {
        var clientInvokeInfos = new List<ClientInvokeInfo>();
        var localAddress = GetLocalAddress();
        if (localAddress.IsNullOrEmpty())
        {
            return ArraySegment<ClientInvokeInfo>.Empty;
        }

        var cacheKeys =
            await _clientInvokeDistributedCache.SearchKeys(
                $"InvokeSupervisor:{localAddress}:*");
        if (cacheKeys.Count <= 0)
        {
            return clientInvokeInfos;
        }

        clientInvokeInfos =
            (await _clientInvokeDistributedCache
                .GetManyAsync(cacheKeys)).Select(p => p.Value)
            .OrderBy(p => p.ServiceEntryId)
            .ToList();
        return clientInvokeInfos;
    }

    public async Task ClearClientInvokeCache()
    {
        var localAddress = GetLocalAddress();
        if (!localAddress.IsNullOrEmpty())
        {
            await RemoveClientInvokeCache(localAddress);
        }

        await _serverInstanceInvokeInfoDistributedCache.RemoveAsync($"InstanceInvokeInfo:{localAddress}");
    }

    public ServerHandleInfo GetServerHandleInfo(string cacheKey)
    {
        if (_serverHandleInvokeCache.TryGetValue(cacheKey, out var serverHandleInfo))
        {
            return serverHandleInfo;
        }

        serverHandleInfo = _serverHandleDistributedCache.Get(cacheKey) ?? new();

        _ = _serverHandleInvokeCache.GetOrAdd(cacheKey, serverHandleInfo);
        return serverHandleInfo;
    }

    public void SetServerHandleInfo(string cacheKey, ServerHandleInfo serverHandleInfo)
    {
        _serverHandleInvokeCache.AddOrUpdate(cacheKey, serverHandleInfo, (k, v) => serverHandleInfo);
    }

    public async Task<IReadOnlyCollection<ServerHandleInfo>> GetServiceEntryHandleInfos()
    {
        var serviceEntryHandleInfos = new List<ServerHandleInfo>();
        var localAddress = GetLocalAddress();
        if (localAddress.IsNullOrEmpty())
        {
            return ArraySegment<ServerHandleInfo>.Empty;
        }

        var cacheKeys =
            await _serverHandleDistributedCache.SearchKeys(
                $"ServerHandleSupervisor:{localAddress}:*");

        if (cacheKeys.Count <= 0)
        {
            return serviceEntryHandleInfos;
        }

        var cacheValues = await _serverHandleDistributedCache.GetManyAsync(cacheKeys);

        var serverHandleInfos = cacheValues.Where(p=> p.Value != null).Select(p => p.Value).ToArray();
        serviceEntryHandleInfos.AddRange(serverHandleInfos);

        return serviceEntryHandleInfos.OrderBy(p => p.ServiceEntryId).ToArray();
    }

    public async Task ClearServerHandleCache()
    {
        var localAddress = GetLocalAddress();
        if (!localAddress.IsNullOrEmpty())
        {
            await RemoveServerHandleCache(localAddress);
        }

        await _serverInstanceHandleInfoDistributedCache.RemoveAsync($"InstanceHandleInfo:{localAddress}");
    }

    private async Task RemoveClientInvokeCache(string address)
    {
        var cacheKeys =
            await _clientInvokeDistributedCache.SearchKeys(
                $"InvokeSupervisor:{address}:*");
        foreach (var cacheKey in cacheKeys)
        {
            await _clientInvokeDistributedCache.RemoveAsync(cacheKey);
        }
    }

    private async Task RemoveServerHandleCache(string address)
    {
        var cacheKeys =
            await _serverHandleDistributedCache.SearchKeys(
                $"ServerHandleSupervisor:{address}:*");
        foreach (var cacheKey in cacheKeys)
        {
            await _serverHandleDistributedCache.RemoveAsync(cacheKey);
        }
    }

    private string GetLocalAddress()
    {
        if (EngineContext.Current.IsContainDotNettyTcpModule())
        {
            var localTcpEndpoint = SilkyEndpointHelper.GetLocalRpcEndpoint();
            return localTcpEndpoint.GetAddress();
        }

        if (EngineContext.Current.IsContainHttpCoreModule())
        {
            var localWebEndpoint = SilkyEndpointHelper.GetLocalWebEndpoint();
            if (localWebEndpoint != null)
            {
                return localWebEndpoint.GetAddress();
            }
        }

        return null;
    }

    public ClientInvokeInfo GetInvokeInfo(string cacheKey)
    {
        if (_clientInvokeCache.TryGetValue(cacheKey, out var clientInvokeInfo))
        {
            return clientInvokeInfo;
        }

        clientInvokeInfo = _clientInvokeDistributedCache.Get(cacheKey) ?? new();

        _ = _clientInvokeCache.GetOrAdd(cacheKey, clientInvokeInfo);
        return clientInvokeInfo;
    }

    public ValueTask DisposeAsync()
    {
        return _timer.DisposeAsync();
    }
}