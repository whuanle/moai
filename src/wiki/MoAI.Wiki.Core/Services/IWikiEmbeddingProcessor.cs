using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Wiki.Services;

/// <summary>
/// 文档向量化处理器抽象.
/// </summary>
public interface IWikiEmbeddingProcessor
{
    /// <summary>
    /// 执行文档向量化.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="isEmbedSourceText">是否向量化原文切片.</param>
    /// <param name="isEmbedMetadata">是否向量化已有元数据.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task ProcessAsync(
        int wikiId,
        int documentId,
        bool isEmbedSourceText,
        bool isEmbedMetadata,
        CancellationToken cancellationToken = default);
}