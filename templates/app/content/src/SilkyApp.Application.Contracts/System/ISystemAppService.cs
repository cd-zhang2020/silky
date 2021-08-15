using Silky.Rpc.Runtime.Server.ServiceDiscovery;
using Silky.Rpc.Security;
using SilkyApp.Application.Contracts.System.Dtos;

namespace SilkyApp.Application.Contracts.System
{
    /// <summary>
    /// 系统信息服务
    /// </summary>
    [ServiceRoute(template:"api/system/{appservice=silkyapp}")]
    public interface ISystemAppService
    {
        /// <summary>
        /// 获取当前系统细信息
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        GetSystemInfoOutput GetInfo();
    }
}