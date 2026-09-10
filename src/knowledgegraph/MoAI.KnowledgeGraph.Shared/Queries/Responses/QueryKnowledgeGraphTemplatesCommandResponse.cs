namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 模板目录响应.
/// </summary>
public class QueryKnowledgeGraphTemplatesCommandResponse
{
    /// <summary>
    /// 模板列表.
    /// </summary>
    public List<KnowledgeGraphTemplateItem> Items { get; init; } = new();
}
