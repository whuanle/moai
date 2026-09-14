using MoAI.App.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// 外部应用可用性校验：外部 token 换取与刷新时保证应用仍为已发布、未禁用的外部应用.
/// </summary>
internal static class ExternalAppAccessValidator
{
    /// <summary>
    /// 校验外部应用可访问，不满足时抛出业务异常.
    /// </summary>
    /// <param name="app">应用实体，可为 null.</param>
    /// <param name="requireNoAuth">要求应用无需授权（is_auth=false），匿名换取 token 时为 true.</param>
    public static void EnsureUsable(AppEntity? app, bool requireNoAuth = false)
    {
        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        if (!app.IsExternal)
        {
            throw new BusinessException("该应用不是外部应用.") { StatusCode = 403 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("该应用已被禁用.") { StatusCode = 403 };
        }

        if (app.PublishStatus != 1)
        {
            throw new BusinessException("该应用未发布.") { StatusCode = 403 };
        }

        if (requireNoAuth && app.IsAuth)
        {
            throw new BusinessException("该应用需要通过应用接入 key 授权访问.") { StatusCode = 403 };
        }
    }

    /// <summary>
    /// 校验 token 上下文来自外部用户（应用 token 无用户身份，不能发起会话等用户级操作）.
    /// </summary>
    /// <param name="context">外部 token 上下文.</param>
    /// <returns>外部用户 id.</returns>
    public static long EnsureExternalUser(ExternalTokenContext context)
    {
        if (context.SubjectType != UserType.External || context.ExternalId <= 0)
        {
            throw new BusinessException("需要外部用户 token（应用 token 无用户身份）.") { StatusCode = 403 };
        }

        return context.ExternalId;
    }
}
