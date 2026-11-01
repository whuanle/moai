using Microsoft.AspNetCore.Http;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统资源协作面（/api/subsys/system/*）守卫：从中间件解析结果取调用方；
/// 资源范围分档已下线，任一系统级接入（子系统/应用接入）均可全量访问本面资源.
/// </summary>
internal static class SystemFaceGuard
{
    /// <summary>
    /// 取系统级接入调用方上下文.
    /// </summary>
    /// <param name="httpContext">HTTP 上下文.</param>
    /// <returns>返回调用方上下文.</returns>
    public static SubsysCallerContext RequireCaller(HttpContext httpContext)
    {
        if (!httpContext.Items.TryGetValue(SubsystemAuthDefaults.CallerContextItemKey, out var value)
            || value is not SubsysCallerContext caller)
        {
            throw new BusinessException("缺少系统级接入调用上下文.") { StatusCode = 401 };
        }

        return caller;
    }
}
