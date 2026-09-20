using System.Threading;
using System.Threading.Tasks;
using MoAI.Wiki.Consumers.Events;

namespace MoAI.Wiki.Services;

/// <summary>
/// 文档工作流处理器抽象：按任务数据编排 AI 切割 → 元数据生成 → 向量化（各步骤可选）.
/// </summary>
public interface IWikiWorkflowProcessor
{
    /// <summary>
    /// 执行文档工作流任务.
    /// </summary>
    /// <param name="data">任务数据（知识库/文档 id 与各步骤选项）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task ProcessAsync(WikiDocumentEmbeddingTaskData data, CancellationToken cancellationToken = default);
}
