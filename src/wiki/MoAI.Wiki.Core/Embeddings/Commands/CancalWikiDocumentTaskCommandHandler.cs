using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Embeddings.Commands;

/// <summary>
/// 取消文档任务.
/// </summary>
public class CancalWikiDocumentTaskCommandHandler : IRequestHandler<CancalWikiDocumentTaskCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancalWikiDocumentTaskCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CancalWikiDocumentTaskCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CancalWikiDocumentTaskCommand request, CancellationToken cancellationToken)
    {
        var documentTask = await _databaseContext.WorkerTasks
            .Where(x => x.BindType == "embedding" && x.BindId == request.DocumentId && x.State <= (int)WorkerState.Processing)
            .FirstOrDefaultAsync();

        if (documentTask == null || documentTask.State >= (int)WorkerState.Processing)
        {
            return EmptyCommandResponse.Default;
        }

        documentTask.State = (int)WorkerState.Cancal;
        documentTask.Message = "任务已取消";

        _databaseContext.WorkerTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
