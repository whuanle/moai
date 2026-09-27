namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用接入项.
/// </summary>
public class AccessAppItem
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid AccessAppId { get; set; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 接入 key（明文；列表可回显，支持再次查看）.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最近使用时间（通过模型网关调用时刷新），未使用为 null.
    /// </summary>
    public DateTimeOffset? LastUsedTime { get; set; }

    /// <summary>
    /// 功能范围代码列表（model/wiki_read/wiki_write/wiki_mcp），model 授权 key 直连使用模型网关，知识库维度决定签发 token 的知识库权限.
    /// </summary>
    public List<string> Scopes { get; set; } = new();
}

/// <summary>
/// 应用接入列表响应.
/// </summary>
public class QueryAccessAppsCommandResponse
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
    /// 接入集合.
    /// </summary>
    public IReadOnlyList<AccessAppItem> Items { get; set; } = new List<AccessAppItem>();
}

/// <summary>
/// 创建应用接入响应，密钥原文仅在创建时返回一次.
/// </summary>
public class CreateAccessAppCommandResponse
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid AccessAppId { get; set; }

    /// <summary>
    /// 密钥原文（仅此一次）.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 密钥展示前缀.
    /// </summary>
    public string KeyPrefix { get; set; } = default!;
}
