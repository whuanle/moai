using MoAI.Infra.Models;

namespace MoAI.App.Models;

/// <summary>
/// 外部 token 上下文：从外部 token claims 解析出的调用方身份与授权范围.
/// </summary>
public class ExternalTokenContext
{
    /// <summary>
    /// 主体类型：应用接入 = <see cref="UserType.ExternalApp"/>，外部用户 = <see cref="UserType.External"/>.
    /// </summary>
    public UserType SubjectType { get; init; }

    /// <summary>
    /// token sub 原文：应用 token 为接入 id，用户 token 为外部用户 id.
    /// </summary>
    public string SubjectId { get; init; } = default!;

    /// <summary>
    /// 外部用户 id（仅用户 token 有值，其余为 0）.
    /// </summary>
    public long ExternalId { get; init; }

    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 来源应用接入 id，匿名 token 为 null.
    /// </summary>
    public Guid? AccessAppId { get; init; }

    /// <summary>
    /// 授权单应用 id（用户 token），应用 token 为 null.
    /// </summary>
    public Guid? AppId { get; init; }

    /// <summary>
    /// 授权应用范围：应用 token 为接入配置的应用列表，用户 token 仅含其绑定的单个应用.
    /// </summary>
    public IReadOnlyList<Guid> AppIds { get; init; } = new List<Guid>();

    /// <summary>
    /// 外部身份标识（用户 token），应用 token 为 null.
    /// </summary>
    public string? ExternalUserId { get; init; }

    /// <summary>
    /// 外部用户显示名（用户 token）.
    /// </summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// 判断 appId 是否在授权范围内.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <returns>是否授权.</returns>
    public bool IsAppAuthorized(Guid appId) => AppIds.Contains(appId);
}
