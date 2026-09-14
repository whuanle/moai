using Microsoft.AspNetCore.Http;
using MoAI.App.Models;
using MoAI.App.Services;

namespace MoAI.App;

/// <summary>
/// 外部 token 上下文读取器：供外部接口控制器从 <see cref="HttpContext"/> 取回认证阶段解析的身份.
/// </summary>
public static class ExternalTokenContextAccessor
{
    /// <summary>
    /// 获取当前请求的外部 token 上下文，未认证时为 null.
    /// </summary>
    /// <param name="httpContext">HTTP 上下文.</param>
    /// <returns>外部 token 上下文.</returns>
    public static ExternalTokenContext? GetExternalTokenContext(this HttpContext httpContext)
    {
        return httpContext.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var value) ? value as ExternalTokenContext : null;
    }
}
