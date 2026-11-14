using MoAI.App.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// 外部应用可用性校验：外部 token 换取与刷新时保证应用仍为已发布、未禁用的外部应用；
/// 访问用户 token（内部应用访问点身份穿透）场景保证目标为已开启访问点的内部应用.
/// </summary>
internal static class ExternalAppAccessValidator
{
    /// <summary>
    /// 校验外部应用可访问（is_auth 已退役，外部应用一律匿名开放），不满足时抛出业务异常.
    /// </summary>
    /// <param name="app">应用实体，可为 null.</param>
    public static void EnsureUsable(AppEntity? app)
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
    }

    /// <summary>
    /// 校验内部应用访问点可访问（访问用户 token 场景）：内部应用、已发布、未禁用、访问点已开启.
    /// </summary>
    /// <param name="app">应用实体，可为 null.</param>
    /// <param name="accessPointEnabled">访问点配置行存在且已启用.</param>
    public static void EnsureInternalUsable(AppEntity? app, bool accessPointEnabled)
    {
        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        if (app.IsExternal)
        {
            throw new BusinessException("外部应用经匿名 token 访问，不走访问用户 token.") { StatusCode = 400 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("该应用已被禁用.") { StatusCode = 403 };
        }

        if (app.PublishStatus != 1)
        {
            throw new BusinessException("该应用未发布.") { StatusCode = 403 };
        }

        if (!accessPointEnabled)
        {
            throw new BusinessException("该应用的访问点未开启.") { StatusCode = 403 };
        }
    }

    /// <summary>
    /// 解析 token 主体的会话身份：访问用户 token → 真实站内用户 id（会话落 UserType.Normal，
    /// 与站内账号合并）；外部用户 token / key 直连身份 → external_user.id（会话落 UserType.External）；
    /// 应用 token 无用户身份，不能发起会话等用户级操作.
    /// </summary>
    /// <param name="context">外部 token 上下文.</param>
    /// <returns>返回会话归属 id 与会话用户类型.</returns>
    public static (long IdentityId, UserType SessionUserType) EnsureSessionIdentity(ExternalTokenContext context)
    {
        if (context.SubjectType == UserType.Normal && context.UserId > 0)
        {
            return (context.UserId, UserType.Normal);
        }

        if (context.ExternalId > 0)
        {
            return (context.ExternalId, UserType.External);
        }

        throw new BusinessException("需要用户 token（应用 token 无用户身份）.") { StatusCode = 403 };
    }

    /// <summary>
    /// 校验 token 上下文携带已解析的外部用户身份（用户 token，或应用接入 key 直连解析出的会话身份；
    /// 应用 token 无用户身份，不能发起会话等用户级操作）.
    /// </summary>
    /// <param name="context">外部 token 上下文.</param>
    /// <returns>外部用户 id.</returns>
    public static long EnsureExternalUser(ExternalTokenContext context)
    {
        if (context.ExternalId <= 0)
        {
            throw new BusinessException("需要外部用户 token（应用 token 无用户身份）.") { StatusCode = 403 };
        }

        return context.ExternalId;
    }
}
