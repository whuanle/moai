using DocumentFormat.OpenXml.Office.CustomUI;
using Maomi.MQ;
using MediatR;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Batch.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Batch.Handlers;

/// <summary>
/// <inheritdoc cref="WikiBatchProcessDocumentCommand"/>
/// </summary>
public class WikiBatchProcessDocumentCommandHandler : IRequestHandler<WikiBatchProcessDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiBatchProcessDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="messagePublisher"></param>
    public WikiBatchProcessDocumentCommandHandler(DatabaseContext databaseContext, IMessagePublisher messagePublisher)
    {
        _databaseContext = databaseContext;
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(WikiBatchProcessDocumentCommand request, CancellationToken cancellationToken)
    {
        var config = request.ToJsonString();
        var entity = await _databaseContext.WorkerTasks.AddAsync(new Database.Entities.WorkerTaskEntity
        {
            BindType = "wiki_batch",
            BindId = request.WikiId,
            State = (int)WorkerState.Wait,
            Message = "Waiting for processing",
            Data = config
        });
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _messagePublisher.AutoPublishAsync(new StartWikiBatchhuMessage
        {
            WikiId = request.WikiId,
            TaskId = entity.Entity.Id,
            Command = request
        });

        return EmptyCommandResponse.Default;
    }
}
