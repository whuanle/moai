using MoAI.Wiki.Batch.Commands;

namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// 知识库批处理任务.
/// </summary>
public class StartWikiBatchhuMessage
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }

    /// <summary>
    /// 命令.
    /// </summary>
    public WikiBatchProcessDocumentCommand Command { get; init; } = default!;
}
