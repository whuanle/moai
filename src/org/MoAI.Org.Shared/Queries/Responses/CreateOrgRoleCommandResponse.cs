namespace MoAI.Org.Queries.Responses;

/// <summary>
/// 创建组织角色结果.
/// </summary>
public class CreateOrgRoleCommandResponse
{
    /// <summary>
    /// 新角色 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 新角色物化路径.
    /// </summary>
    public string Path { get; init; } = string.Empty;
}
