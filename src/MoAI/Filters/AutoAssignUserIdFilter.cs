using Maomi;
using Microsoft.AspNetCore.Mvc.Filters;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Filters;

/// <summary>
/// 自动赋值 IUserIdContext
/// </summary>
[InjectOnScoped]
public class AutoAssignUserIdFilter : IAsyncActionFilter
{
    private readonly IUserContextProvider _userContextProvider;
    private readonly IClientInfoProvider _clientInfoProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoAssignUserIdFilter"/> class.
    /// </summary>
    /// <param name="userContextProvider"></param>
    /// <param name="clientInfoProvider"></param>
    public AutoAssignUserIdFilter(IUserContextProvider userContextProvider, IClientInfoProvider clientInfoProvider)
    {
        _userContextProvider = userContextProvider;
        _clientInfoProvider = clientInfoProvider;
    }

    /// <summary>
    /// 动作执行前的处理逻辑
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="next">下一个过滤器委托</param>
    /// <returns></returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userContext = _userContextProvider.GetUserContext();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is IUserIdContext userIdContext)
            {
                userIdContext.SetUserId(userContext.UserId);
                userIdContext.SetProperty(a => a.ContextUserType, userContext.UserType);
            }

            // 匿名接口（如登录）同样回填客户端信息，供登录风控等场景使用
            if (argument is IClientContext clientContext)
            {
                var clientInfo = _clientInfoProvider.GetClientInfo();
                clientContext.SetClientInfo(clientInfo.IP, clientInfo.UserAgent);
            }
        }

        await next();
    }
}