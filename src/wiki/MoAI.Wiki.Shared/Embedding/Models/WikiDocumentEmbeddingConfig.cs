using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Wiki.Embedding.Models;

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
    /// 向量化衍生数据.
    /// </summary>
    public bool IsEmbedDerivedData { get; init; } = true;
}
