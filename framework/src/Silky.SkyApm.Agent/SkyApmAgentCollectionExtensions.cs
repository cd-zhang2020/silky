using Silky.Rpc.SkyApm.Diagnostics;
using Silky.SkyApm.Agent.Configuration;
using SkyApm.Utilities.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SkyApmAgentCollectionExtensions
    {
        public static IServiceCollection AddSilkySkyApm(this IServiceCollection services)
        {
            services.AddSkyAPM(extensions =>
            {
                extensions.AddSilkyRpc();
                extensions.Services.AddSingleton<IConfigurationFactory, SilkyConfigurationFactory>();
            });
            return services;
        }
    }
}