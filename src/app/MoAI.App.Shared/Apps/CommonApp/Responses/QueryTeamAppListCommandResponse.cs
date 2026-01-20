using MoAI.Storage.Queries;

namespace MoAI.App.Apps.CommonApp.Responses;

/// <summary>
/// 团队应用列表响应.
/// </summary>
public class QueryTeamAppListCommandResponse
{
    /// <summary>
    /// 应用列表.
    /// </summary>
    public IReadOnlyCollection<TeamAppItem> Items { get; init; } = Array.Empty<TeamAppItem>();
}

/// <summary>
/// 团队应用项.
/// </summary>
public class TeamAppItem : Infra.Models.AuditsInfo, IAvatarPath
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 头像 objectKey.
    /// </summary>
    public string AvatarKey { get; set; } = string.Empty;

    /// <summary>
    /// 头像.
    /// </summary>
    public string Avatar { get; set; } = string.Empty;

    /// <summary>
    /// 应用类型，普通应用=0,流程编排=1.
    /// </summary>
    public int AppType { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <summary>
    /// 是否外部应用.
    /// </summary>
    public bool IsForeign { get; init; }
}
