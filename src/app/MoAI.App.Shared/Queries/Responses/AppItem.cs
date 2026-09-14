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
    /// 是否外部应用.
    /// </summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// 是否需要授权访问（仅外部应用有效）.
    /// </summary>
    public bool IsAuth { get; set; }

    /// <summary>
    /// 是否公开到平台（仅内部应用有效）.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 发布状态：0=草稿（未发布）1=已发布.
    /// </summary>
    public short PublishStatus { get; set; }

    /// <summary>
    /// 发布时间，未发布为 null.
    /// </summary>
    public DateTimeOffset? PublishTime { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
