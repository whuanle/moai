using MoAI.App.Models;
using MoAI.Infra.Models;
using MoAI.Storage.Queries;

namespace MoAI.App.Manager.Models;

/// <summary>
/// 应用列表响应.
/// </summary>
public class QueryTeamAppListCommandResponse
{
    /// <summary>
    /// 应用列表.
    /// </summary>
    public IReadOnlyCollection<QueryAppListCommandResponseItem> Items { get; set; } = Array.Empty<QueryAppListCommandResponseItem>();
}

/// <summary>
/// 应用列表项.
/// </summary>
public class QueryAppListCommandResponseItem : AuditsInfo, IAvatarPath
{
    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Avatar { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string AvatarKey { get; set; } = string.Empty;

    /// <summary>
    /// 是否外部应用.
    /// </summary>
    public bool IsForeign { get; set; }

    /// <summary>
    /// 应用类型.
    /// </summary>
    public AppType AppType { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; set; }
}
