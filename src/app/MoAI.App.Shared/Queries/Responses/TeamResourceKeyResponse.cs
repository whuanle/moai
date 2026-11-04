using MoAI.Database.Enums;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// 团队资源 key 项（一个资源一把 key：应用 ACP/A2A 共用其应用的 key，知识库/知识图谱各一把）.
/// </summary>
public class TeamResourceKeyItem
{
    /// <summary>
    /// 资源 key 行 id.
    /// </summary>
    public Guid TeamResourceKeyId { get; set; }

    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 资源类型：app=团队应用（ACP/A2A）/ wiki=知识库 / knowledge_graph=知识图谱.
    /// </summary>
    public TeamResourceType ResourceType { get; set; }

    /// <summary>
    /// 资源 id（字符串形态）：app 行=应用 Guid、wiki 行=wikiId、knowledge_graph 行=kgId.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// key 原文（moai-tr- 前缀，明文回显，仅团队 Admin+ 可见）.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// key 展示前缀.
    /// </summary>
    public string KeyPrefix { get; set; } = default!;

    /// <summary>
    /// 状态：enabled/disabled.
    /// </summary>
    public TeamResourceKeyStatus Status { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}

/// <summary>
/// 团队资源 key 列表响应.
/// </summary>
public class QueryTeamResourceKeysCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 我在该团队中的角色：0=Member 1=Admin 2=Owner.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 资源 key 集合.
    /// </summary>
    public IReadOnlyList<TeamResourceKeyItem> Items { get; set; } = new List<TeamResourceKeyItem>();
}

/// <summary>
/// 生成团队资源 key 响应（get-or-create）：新生成时 <c>Created</c>=true；资源未开启对外功能时拒绝生成（400）.
/// </summary>
public class CreateTeamResourceKeyCommandResponse
{
    /// <summary>
    /// 资源 key 行 id.
    /// </summary>
    public Guid TeamResourceKeyId { get; set; }

    /// <summary>
    /// 资源类型.
    /// </summary>
    public TeamResourceType ResourceType { get; set; }

    /// <summary>
    /// 资源 id（字符串形态）.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// key 原文（moai-tr- 前缀，明文回显）.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// key 展示前缀.
    /// </summary>
    public string KeyPrefix { get; set; } = default!;

    /// <summary>
    /// 是否新生成（false=已存在原样返回）.
    /// </summary>
    public bool Created { get; set; }
}

/// <summary>
/// 重置团队资源 key 响应，key 原文仅此一次返回.
/// </summary>
public class ResetTeamResourceKeyCommandResponse
{
    /// <summary>
    /// 资源 key 行 id.
    /// </summary>
    public Guid TeamResourceKeyId { get; set; }

    /// <summary>
    /// 新 key 原文.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// key 展示前缀.
    /// </summary>
    public string KeyPrefix { get; set; } = default!;
}

/// <summary>
/// 应用对外功能（ACP/A2A）与团队资源 key 响应：一个应用一把 key（ACP/A2A 共用），
/// 未开启任一功能时 key 相关字段为 null.
/// </summary>
public class AppFeatureResponse
{
    /// <summary>
    /// 是否开启 ACP 接入.
    /// </summary>
    public bool IsAcpEnabled { get; set; }

    /// <summary>
    /// 是否开启 A2A 接入.
    /// </summary>
    public bool IsA2aEnabled { get; set; }

    /// <summary>
    /// 资源 key 行 id（未生成时 null）.
    /// </summary>
    public Guid? TeamResourceKeyId { get; set; }

    /// <summary>
    /// key 原文（moai-tr- 前缀，未生成时 null）.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// key 展示前缀（未生成时 null）.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// key 状态：enabled/disabled（未生成时 null）.
    /// </summary>
    public TeamResourceKeyStatus? Status { get; set; }
}
