using System.Collections.Generic;
using System.Threading.Tasks;
using Consul;
using Silky.Core.Extensions;
using Silky.Rpc.Endpoint.Descriptor;
using Silky.Rpc.Runtime.Server;

namespace Silky.RegistryCenter.Consul
{
    public class ConsulServerConverter : IServerConverter
    {
        private readonly IServiceProvider _serviceProvider;

        public ConsulServerConverter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<ServerDescriptor> Convert(string serverName, AgentService[] agentServices)
        {
            var serverDescriptor = new ServerDescriptor()
            {
                HostName = serverName,
                Services = await _serviceProvider.GetServices(serverName)
            };
            var endpoints = new List<SilkyEndpointDescriptor>();
            foreach (var agentService in agentServices)
            {
                var rpcEndpointDescriptor = agentService.GetEndpointDescriptors();
                endpoints.AddRange(rpcEndpointDescriptor);
            }

            serverDescriptor.Endpoints = endpoints.ToArray();
            return serverDescriptor;
        }
    }
}