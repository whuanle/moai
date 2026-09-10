using System;
using System.Collections.Generic;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 团队可用模型选项响应.
/// </summary>
public class QueryWikiModelOptionsCommandResponse
{
    /// <summary>
    /// 可用的向量化模型列表.
    /// </summary>
    public List<WikiModelOptionItem> EmbeddingModels { get; set; } = new();

    /// <summary>
    /// 可用的对话模型列表（用于元数据生成）.
    /// </summary>
    public List<WikiModelOptionItem> ConversationModels { get; set; } = new();

    /// <summary>
    /// 可用的重排序模型列表（知识库可选配置）.
    /// </summary>
    public List<WikiModelOptionItem> RerankModels { get; set; } = new();
}

/// <summary>
/// 团队可用模型选项项.
/// </summary>
public class WikiModelOptionItem
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 模型类型（conversation/embedding）.
    /// </summary>
    public string ModelKind { get; set; } = default!;
}
