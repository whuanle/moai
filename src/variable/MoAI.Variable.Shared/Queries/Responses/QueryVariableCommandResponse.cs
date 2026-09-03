namespace MoAI.Variable.Queries.Responses;

/// <summary>
/// 变量详情响应.
/// </summary>
public class QueryVariableCommandResponse
{
    /// <summary>
    /// 变量 id.
    /// </summary>
    public long VariableId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 变量名.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 分组名，空串=未分组.
    /// </summary>
    public string GroupName { get; set; } = default!;

    /// <summary>
    /// 是否私密变量.
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// 变量值；私密变量仅管理员可见（成员访问返回 403）.
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// 变量描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
