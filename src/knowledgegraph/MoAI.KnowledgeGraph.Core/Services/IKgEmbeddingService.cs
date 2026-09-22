using System.Threading;
using System.Threading.Tasks;
using MoAI.KnowledgeGraph.Consumers.Events;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱节点向量增量处理：按 delta 消息重建/删除节点向量（幂等，upsert 读图库最新状态）.
/// </summary>
public interface IKgEmbeddingService
{
    /// <summary>
    /// 处理节点向量增量：删除直接删向量，重建读图库最新状态后重嵌.
    /// 单节点失败不影响其余节点；全部失败抛出最后异常以触发 MQ 重投.
    /// </summary>
    /// <param name="message">增量消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task ProcessDeltaAsync(KgNodeEmbeddingDeltaMessage message, CancellationToken cancellationToken);
}
