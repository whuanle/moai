namespace MoAI.Variable.Queries.Responses;

/// <summary>
/// 团队变量项；私密变量的 <see cref="Value"/> 对成员恒为 null.
/// </summary>
public class TeamVariableItem
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
    /// 变量值；私密变量恒为 null（仅管理员在详情接口可见）.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// 变量描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
