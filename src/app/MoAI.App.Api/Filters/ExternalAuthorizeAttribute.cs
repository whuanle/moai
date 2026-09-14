using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MoAI.App.Services;

namespace MoAI.App.Filters;

/// <summary>
/// 外部接口拦截器：要求请求携带有效的外部 token（<see cref="ExternalAuthDefaults.AuthenticationScheme"/>），
/// 校验通过后把解析出的 <see cref="Models.ExternalTokenContext"/> 提供给控制器.
/// ExternalController 整体标 [AllowAnonymous]（规避全局 /api 前缀 convention 自动补 [Authorize] 与内部用户态中间件），
/// 受保护端点必须显式标注本特性.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ExternalAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <inheritdoc/>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var authenticateResult = await httpContext.AuthenticateAsync(ExternalAuthDefaults.AuthenticationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            context.Result = new JsonResult(new
            {
                error = new
                {
                    message = "Invalid or missing external token.",
                    type = "authentication_error",
                    code = "invalid_token",
                }
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
            return;
        }

        // 与管道中的 HttpContext.User（内部 JWT/匿名）隔离，外部身份只通过 token context 传递
        httpContext.User = authenticateResult.Principal;
    }
}
