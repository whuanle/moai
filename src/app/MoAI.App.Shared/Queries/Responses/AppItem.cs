using MoAI.Database.Enums;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用项.
/// </summary>
public class AppItem
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 应用描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 应用类型：Agent 应用=0，流程应用=1.
    /// </summary>
    public AppType AppType { get; set; }

    /// <summary>
    /// 应用头像的 ObjectKey（空串=未设置）.
    /// </summary>
    public string AvatarPath { get; set; } = default!;

    /// <summary>
    /// 允许外部使用.
    /// </summary>
    public bool EnableForeign { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
