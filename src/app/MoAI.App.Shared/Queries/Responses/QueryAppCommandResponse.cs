using MoAI.Database.Enums;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用详情响应.
/// </summary>
public class QueryAppCommandResponse
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
    /// 对话开场白（仅 Agent 应用，取自应用配置），未配置为空串.
    /// </summary>
    public string OpeningStatement { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用对话开场白（仅 Agent 应用）；启用且内容非空时，新会话开始时展示.
    /// </summary>
    public bool OpeningStatementEnabled { get; set; }

    /// <summary>
    /// 快捷输入列表（管理员配置，对话欢迎态点击即发送），未配置为空列表.
    /// </summary>
    public IReadOnlyList<string> QuickInputs { get; set; } = new List<string>();

    /// <summary>
    /// 我在所属团队中的角色：0=Member 1=Admin 2=Owner.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
