using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Batch.Commands;

/// <summary>
/// 删除或取消批处理文档.
/// </summary>
public class DeleteBatchProcessDocumentCommand : IRequest<EmptyCommandResponse>
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
    /// 取消任务.
    /// </summary>
    public bool IsCancal { get; init; } = true;

    /// <summary>
    /// 取消并删除任务.
    /// </summary>
    public bool IsDelete { get; init; }
}
