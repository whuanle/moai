using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Wiki.Batch.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Batch.Queries;

/// <summary>
/// 查询知识库批处理任务.
/// </summary>
public class QueryWikiBatchProcessDocumentListCommandHandler : IRequestHandler<QueryWikiBatchProcessDocumentListCommand, QueryWikiBatchProcessDocumentListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiBatchProcessDocumentListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiBatchProcessDocumentListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiBatchProcessDocumentListCommandResponse> Handle(QueryWikiBatchProcessDocumentListCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.WorkerTasks
            .Where(x => x.BindId == request.WikiId && x.BindType == "wiki_batch")
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new WikiBatchProcessDocumenItem
            {
                TaskId = x.Id,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                Data = x.Data,
                Message = x.Message,
                State = (WorkerState)x.State,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId
            })
            .ToListAsync(cancellationToken);

        return new QueryWikiBatchProcessDocumentListCommandResponse
        {
            Items = items,
        };
    }
}
