using Microsoft.AspNetCore.Http;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统资源协作面（/api/subsys/system/*）守卫：从中间件解析结果取调用方并做 scope 分档校验.
/// </summary>
internal static class SystemFaceGuard
{
    /// <summary>
    /// 取系统应用接入调用方上下文，并校验所需 scope（写档端点传对应写范围，读档端点传对应读范围）.
    /// </summary>
    /// <param name="httpContext">HTTP 上下文.</param>
    /// <param name="requiredScope">所需功能范围，None 表示仅要求身份.</param>
    /// <returns>返回调用方上下文.</returns>
    public static SystemAccessAppCallerContext RequireCaller(HttpContext httpContext, SystemApiKeyScopes requiredScope = SystemApiKeyScopes.None)
    {
        if (!httpContext.Items.TryGetValue(SystemAccessAppAuthDefaults.CallerContextItemKey, out var value)
            || value is not SystemAccessAppCallerContext caller)
        {
            throw new BusinessException("缺少系统应用接入调用上下文.") { StatusCode = 401 };
        }

        if (requiredScope != SystemApiKeyScopes.None && !caller.Scopes.HasFlag(requiredScope))
        {
            throw new BusinessException("系统应用接入未被授予所需功能范围.") { StatusCode = 403 };
        }

        return caller;
    }
}
