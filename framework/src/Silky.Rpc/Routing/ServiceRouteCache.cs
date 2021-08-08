﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Silky.Core;
using Silky.Core.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Silky.Rpc.Address;
using Silky.Rpc.Address.HealthCheck;
using Silky.Rpc.Routing.Descriptor;
using Silky.Rpc.Runtime.Server;

namespace Silky.Rpc.Routing
{
    public class ServiceRouteCache : ISingletonDependency
    {
        private readonly ConcurrentDictionary<string, ServiceRoute> _serviceRouteCache = new();
        private readonly IHealthCheck _healthCheck;
        private readonly IServiceEntryLocator _serviceEntryLocator;
        private readonly IServiceEntryManager _serviceEntryManager;

        public ILogger<ServiceRouteCache> Logger { get; set; }

        public event OnRemoveServiceRoutes OnRemoveServiceRoutes;

        public ServiceRouteCache(IHealthCheck healthCheck,
            IServiceEntryLocator serviceEntryLocator,
            IServiceEntryManager serviceEntryManager)
        {
            _healthCheck = healthCheck;
            _serviceEntryLocator = serviceEntryLocator;
            _serviceEntryManager = serviceEntryManager;
            _healthCheck.OnRemveAddress += OnRemoveAddressHandler;
            Logger = NullLogger<ServiceRouteCache>.Instance;
        }

        private async Task OnRemoveAddressHandler(IAddressModel addressmodel)
        {
            addressmodel.InitFuseTimes();
            var remveAddressServiceRoutes =
                ServiceRoutes.Where(p => p.Addresses.Any(q => q.Descriptor == addressmodel.Descriptor));
            var updateRegisterServiceRouteDescriptors = new List<ServiceRouteDescriptor>();
            foreach (var remveAddressServiceRoute in remveAddressServiceRoutes)
            {
                remveAddressServiceRoute.Addresses =
                    remveAddressServiceRoute.Addresses.Where(p => p.Descriptor != addressmodel.Descriptor).ToArray();
                _serviceRouteCache.AddOrUpdate(remveAddressServiceRoute.ServiceDescriptor.Id,
                    remveAddressServiceRoute, (id, _) => remveAddressServiceRoute);
                updateRegisterServiceRouteDescriptors.Add(remveAddressServiceRoute.ConvertToDescriptor());
            }

            OnRemoveServiceRoutes?.Invoke(updateRegisterServiceRouteDescriptors, addressmodel);
        }


        public void UpdateCache([NotNull] ServiceRouteDescriptor serviceRouteDescriptor)
        {
            Check.NotNull(serviceRouteDescriptor, nameof(serviceRouteDescriptor));


            var serviceRoute = serviceRouteDescriptor.ConvertToServiceRoute();
            _serviceRouteCache.AddOrUpdate(serviceRouteDescriptor.ServiceDescriptor.Id,
                serviceRoute, (id, _) => serviceRoute);

            Logger.LogDebug(
                $"Update the service routing [{serviceRoute.ServiceDescriptor.Id}] cache, the routing address is:[{string.Join(',', serviceRoute.Addresses.Select(p => p.ToString()))}]");

            foreach (var address in serviceRoute.Addresses)
            {
                _healthCheck.Monitor(address);
            }

            var serviceEntry = _serviceEntryLocator.GetServiceEntryById(serviceRouteDescriptor.ServiceDescriptor.Id);
            if (serviceEntry != null)
            {
                if (serviceEntry.FailoverCountIsDefaultValue)
                {
                    serviceEntry.GovernanceOptions.FailoverCount = serviceRouteDescriptor.AddressDescriptors.Count();
                    _serviceEntryManager.Update(serviceEntry);
                }
            }
        }

        public void RemoveCache(string serviceId)
        {
            _serviceRouteCache.TryRemove(serviceId, out ServiceRoute serviceRoute);
            if (serviceRoute != null)
            {
                foreach (var routeAddress in serviceRoute.Addresses)
                {
                    _healthCheck.RemoveAddress(routeAddress);
                }
            }
        }

        public IReadOnlyList<ServiceRouteDescriptor> ServiceRouteDescriptors =>
            _serviceRouteCache.Values.Select(p => p.ConvertToDescriptor()).ToImmutableArray();

        public IReadOnlyList<ServiceRoute> ServiceRoutes => _serviceRouteCache.Values.ToImmutableArray();

        public ServiceRoute GetServiceRoute(string serviceId)
        {
            if (_serviceRouteCache.TryGetValue(serviceId, out ServiceRoute serviceRoute))
            {
                return serviceRoute;
            }

            return null;
        }
    }
}