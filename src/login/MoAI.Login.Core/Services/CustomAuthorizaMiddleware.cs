
#pragma warning disable CA1031 // 不捕获常规异常类型
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Login.Queries;

namespace MoAI.Login.Services;

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
            try
            {
                var userState = await _mediator.Send(new QueryUserStateCommand
                {
                    UserId = userContext.UserId
                });

                if (userState.IsDeleted || userState.IsDisable)
                {
                    throw new BusinessException("账号已被禁用");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Can't query user state，user id: {UserId}", userContext.UserId);
            }
        }

        // todo: 判断用户状态.
        await next(context);
    }
}