namespace MoAI.App.Queries.Responses;

/// <summary>应用用量汇总.</summary>
public class AppUsageSummary
{
    /// <summary>调用次数.</summary>
    public long CallCount { get; set; }
    /// <summary>输入 token.</summary>
    public long PromptTokens { get; set; }
    /// <summary>输出 token.</summary>
    public long CompletionTokens { get; set; }
    /// <summary>合计 token.</summary>
    public long TotalTokens { get; set; }
    /// <summary>缓存命中的输入 token（模型响应 usage 上报，未上报为 0）.</summary>
    public long CachedTokens { get; set; }
}

/// <summary>应用按模型用量.</summary>
public class AppUsageModelItem
{
    /// <summary>模型 id.</summary>
    public Guid ModelId { get; set; }
    /// <summary>模型名称（模型已删除时回落为 id 字符串）.</summary>
    public string ModelName { get; set; } = string.Empty;
    /// <summary>调用次数.</summary>
    public long CallCount { get; set; }
    /// <summary>输入 token.</summary>
    public long PromptTokens { get; set; }
    /// <summary>输出 token.</summary>
    public long CompletionTokens { get; set; }
    /// <summary>合计 token.</summary>
    public long TotalTokens { get; set; }
    /// <summary>缓存命中的输入 token（模型响应 usage 上报，未上报为 0）.</summary>
    public long CachedTokens { get; set; }
}

/// <summary>应用使用活动统计（会话/用户/消息维度）.</summary>
public class AppUsageActivityStats
{
    /// <summary>累计使用次数（会话创建计数，删除会话不减少，Redis 计数器异步累加）.</summary>
    public long UseCount { get; set; }

    /// <summary>会话总数（正式会话，不含已删除）.</summary>
    public int SessionCount { get; set; }

    /// <summary>使用用户数（按会话归属用户去重，外部用户单独计数）.</summary>
    public int UserCount { get; set; }

    /// <summary>消息总数（会话落库消息，压缩后视图）.</summary>
    public long MessageCount { get; set; }

    /// <summary>最近使用时间（最后一条会话消息时间），无会话为 null.</summary>
    public DateTimeOffset? LastActiveTime { get; set; }
}

/// <summary>应用用量统计结果（汇总 + 按模型分布 + 使用活动统计）.</summary>
public class QueryAppUsageCommandResponse
{
    /// <summary>用量汇总.</summary>
    public AppUsageSummary Summary { get; set; } = new();
    /// <summary>按模型分布（按合计 token 倒序）.</summary>
    public IReadOnlyList<AppUsageModelItem> ByModel { get; set; } = new List<AppUsageModelItem>();
    /// <summary>使用活动统计.</summary>
    public AppUsageActivityStats Stats { get; set; } = new();
}
