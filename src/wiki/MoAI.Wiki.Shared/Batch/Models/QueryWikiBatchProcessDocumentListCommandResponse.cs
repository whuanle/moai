using MediatR;
using MoAI.Wiki.Batch.Models;

namespace MoAI.Wiki.Batch.Queries;

/// <summary>
/// 任务.
/// </summary>
public class QueryWikiBatchProcessDocumentListCommandResponse
{
    /// <summary>
    /// 任务列表.
    /// </summary>
    public IReadOnlyCollection<WikiBatchProcessDocumenItem> Items { get;init; } = Array.Empty<WikiBatchProcessDocumenItem>();
}
