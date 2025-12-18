using MediatR;

namespace MoAI.Wiki.Batch.Queries;

/// <summary>
/// 查询知识库批处理任务.
/// </summary>
public class QueryWikiBatchProcessDocumentListCommand : IRequest<QueryWikiBatchProcessDocumentListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }
}