#pragma warning disable CA1031 // 不捕获常规异常类型

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Account.Services;

/// <summary>
/// 鉴权中间件.
/// </summary>
public class CustomAuthorizaMiddleware : IMiddleware
{
    private readonly IUserContextProvider _userContextProvider;
    private readonly ILogger<CustomAuthorizaMiddleware> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomAuthorizaMiddleware"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="userContextProvider"></param>
    /// <param name="mediator"></param>
    public CustomAuthorizaMiddleware(ILogger<CustomAuthorizaMiddleware> logger, IUserContextProvider userContextProvider, IMediator mediator)
    {
        _logger = logger;
        _userContextProvider = userContextProvider;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var userContext = _userContextProvider.GetUserContext();

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await next(context);
            return;
        }

        // todo: 后续添加权限验证逻辑
        var authorizeData = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? Array.Empty<IAuthorizeData>();

        if (authorizeData.Count > 0)
        {
            UserStateInfo? userState = null;
            try
            {
                userState = await _mediator.Send(new QueryUserStateCommand
                {
                    UserId = userContext.UserId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Can't query user state，user id: {UserId}", userContext.UserId);
            }

            // 禁用/删除的账号立即拦截；用户态查询失败时保持放行，维持原有容错语义.
            if (userState != null && (userState.IsDeleted || userState.IsDisable))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new BusinessValidationResult
                {
                    Code = StatusCodes.Status403Forbidden,
                    RequestId = context.TraceIdentifier,
                    Detail = "账号已被禁用",
                });
                return;
            }
        }

        await next(context);
    }
}