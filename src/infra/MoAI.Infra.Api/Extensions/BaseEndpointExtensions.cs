using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;

namespace MoAI.Infra.Extensions;

/// <summary>
/// BaseEndpointExtensions.
/// </summary>
public static class BaseEndpointExtensions
{
    /// <summary>
    /// 配置用户上下文.
    /// </summary>
    /// <typeparam name="T">.</typeparam>
    /// <param name="endpoint"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public static T ConfigureUserId<T>(this BaseEndpoint endpoint, T model)
        where T : class, IUserIdContext
    {
        var userContext = endpoint.HttpContext.RequestServices.GetRequiredService<UserContext>();
        model.SetProperty(a => a.ContextUserId, userContext.UserId);
        return model;
    }
}
