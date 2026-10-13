namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用安全事件（一条规则命中聚合行）.
/// </summary>
public class AppSecurityEventItem
{
    /// <summary>事件 id.</summary>
    public Guid Id { get; set; }

    /// <summary>会话 id，空为无会话上下文.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>触发用户 id，空为无用户身份.</summary>
    public long? UserId { get; set; }

    /// <summary>作用域：input/toolResult/toolArgs/modelOutput.</summary>
    public string Scope { get; set; } = default!;

    /// <summary>规则名称.</summary>
    public string RuleName { get; set; } = default!;

    /// <summary>规则类型：phone/idCard/email/bankCard/custom.</summary>
    public string RuleType { get; set; } = default!;

    /// <summary>处理策略：redact/mask/hash/block/flag.</summary>
    public string Strategy { get; set; } = default!;

    /// <summary>动作：masked/blocked/flagged.</summary>
    public string Action { get; set; } = default!;

    /// <summary>聚合命中次数.</summary>
    public int HitCount { get; set; }

    /// <summary>发生时间.</summary>
    public DateTimeOffset CreateTime { get; set; }
}

/// <summary>
/// 应用安全事件分页结果.
/// </summary>
public class QueryAppSecurityEventsCommandResponse
{
    /// <summary>事件条目.</summary>
    public IReadOnlyList<AppSecurityEventItem> Items { get; set; } = new List<AppSecurityEventItem>();

    /// <summary>总数量.</summary>
    public int Total { get; set; }

    /// <summary>页码.</summary>
    public int PageNo { get; set; }

    /// <summary>每页数量.</summary>
    public int PageSize { get; set; }
}
