namespace MoAI.Variable.Queries.Responses;

/// <summary>
/// 团队变量列表响应.
/// </summary>
public class QueryVariablesCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 我在该团队中的角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 变量集合.
    /// </summary>
    public IReadOnlyList<TeamVariableItem> Items { get; set; } = new List<TeamVariableItem>();
}
