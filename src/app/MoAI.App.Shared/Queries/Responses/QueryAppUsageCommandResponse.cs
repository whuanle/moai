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
}

/// <summary>应用用量统计结果（汇总 + 按模型分布）.</summary>
public class QueryAppUsageCommandResponse
{
    /// <summary>用量汇总.</summary>
    public AppUsageSummary Summary { get; set; } = new();
    /// <summary>按模型分布（按合计 token 倒序）.</summary>
    public IReadOnlyList<AppUsageModelItem> ByModel { get; set; } = new List<AppUsageModelItem>();
}
