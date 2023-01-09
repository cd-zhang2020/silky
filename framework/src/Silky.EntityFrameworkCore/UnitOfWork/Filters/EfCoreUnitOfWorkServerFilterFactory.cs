using System;
using Microsoft.Extensions.DependencyInjection;
using Silky.Core.DbContext;
using Silky.Core.DependencyInjection;
using Silky.Core.FilterMetadata;
using Silky.Rpc.Filters;

namespace Silky.EntityFrameworkCore.UnitOfWork;

public class EfCoreUnitOfWorkServerFilterFactory : IServerFilterFactory, ISingletonDependency
{
    public bool IsReusable { get; } = false;
    
    public IServerFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new EfCoreUnitOfWorkServerFilter(serviceProvider.GetRequiredService<ISilkyDbContextPool>());
    }
}