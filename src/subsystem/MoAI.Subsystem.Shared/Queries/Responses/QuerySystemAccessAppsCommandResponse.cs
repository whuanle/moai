using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 系统应用接入列表.
/// </summary>
public class QuerySystemAccessAppsCommandResponse
{
    /// <summary>
    /// 总数.
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// 列表项.
    /// </summary>
    public IReadOnlyList<QuerySystemAccessAppsCommandResponseItem> Items { get; init; } = [];
}

/// <summary>
/// 系统应用接入列表项.
/// </summary>
public class QuerySystemAccessAppsCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// key 展示前缀.
    /// </summary>
    public string KeyPrefix { get; init; } = default!;

    /// <summary>
    /// 功能范围代码列表.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// 状态：enabled/disabled.
    /// </summary>
    public AccessAppStatus Status { get; init; }
}

/// <summary>
/// 系统应用接入自省结果.
/// </summary>
public class QuerySystemAccessAppInfoCommandResponse
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 功能范围代码列表.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// 状态：enabled/disabled.
    /// </summary>
    public AccessAppStatus Status { get; init; }
}
