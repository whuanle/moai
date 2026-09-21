namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 团队可用 AI 对话模型选项响应.
/// </summary>
public class QueryKnowledgeGraphModelOptionsCommandResponse
{
    /// <summary>
    /// 可用的对话模型列表（用于 AI 导入文件）.
    /// </summary>
    public List<KnowledgeGraphModelOptionItem> ConversationModels { get; init; } = new();
}

/// <summary>
/// 团队可用模型选项项.
/// </summary>
public class KnowledgeGraphModelOptionItem
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; set; } = default!;
}
