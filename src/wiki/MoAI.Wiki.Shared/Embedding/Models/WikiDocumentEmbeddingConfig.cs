using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Wiki.Embedding.Models;

/// <summary>
/// 文档向量化配置.
/// </summary>
public class WikiDocumentEmbeddingConfig
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 是否将 chunk 源文本也向量化.
    /// </summary>
    public bool IsEmbedSourceText { get; init; } = true;

    /// <summary>
    /// 并发线程数量.
    /// </summary>
    public int ThreadCount { get; init; } = 5;
}
