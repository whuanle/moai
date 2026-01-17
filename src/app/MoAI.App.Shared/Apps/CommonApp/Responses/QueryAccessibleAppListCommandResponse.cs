using MoAI.Storage.Queries;

namespace MoAI.App.Apps.CommonApp.Responses;

/// <summary>
/// 用户可访问应用列表响应.
/// </summary>
public class QueryAccessibleAppListCommandResponse
{
    /// <summary>
    /// 应用列表.
    /// </summary>
    public IReadOnlyCollection<AccessibleAppItem> Items { get; init; } = Array.Empty<AccessibleAppItem>();
}

/// <summary>
/// 用户可访问的应用项.
/// </summary>
public class AccessibleAppItem : IAvatarPath
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
    /// 所属团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 所属团队名称.
    /// </summary>
    public string TeamName { get; init; } = string.Empty;

    /// <summary>
    /// 是否公开应用.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }
}
