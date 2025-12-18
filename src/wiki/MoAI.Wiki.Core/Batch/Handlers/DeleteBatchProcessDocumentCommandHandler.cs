using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Batch.Commands;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Batch.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteBatchProcessDocumentCommand"/>
/// </summary>
public class DeleteBatchProcessDocumentCommandHandler : IRequestHandler<DeleteBatchProcessDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteBatchProcessDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteBatchProcessDocumentCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteBatchProcessDocumentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WorkerTasks.FirstOrDefaultAsync(x => x.Id == request.TaskId && x.BindId == request.WikiId);
        if (entity == null)
        {
            throw new BusinessException("任务不存在");
        }

        entity.State = (int)WorkerState.Cancal;
        _databaseContext.WorkerTasks.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (request.IsDelete)
        {
            _databaseContext.WorkerTasks.Remove(entity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
