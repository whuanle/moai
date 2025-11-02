using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// <inheritdoc cref="StartWebDocumentCrawleCommand"/>
/// </summary>
public class StartWebDocumentCrawleCommandHandler : IRequestHandler<StartWebDocumentCrawleCommand, SimpleGuid>
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWebDocumentCrawleCommandHandler"/> class.
    /// </summary>
    /// <param name="messagePublisher"></param>
    /// <param name="databaseContext"></param>
    public StartWebDocumentCrawleCommandHandler(IMessagePublisher messagePublisher, DatabaseContext databaseContext)
    {
        _messagePublisher = messagePublisher;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(StartWebDocumentCrawleCommand request, CancellationToken cancellationToken)
    {
        var existTask = await _databaseContext.WikiWebCrawleTasks
            .AnyAsync(x => x.WikiId == request.WikiId && x.WikiWebConfigId == request.WebConfigId && x.CrawleState < (int)CrawleState.Cancal, cancellationToken);

        if (existTask == true)
        {
            throw new BusinessException("当前知识库网页爬取任务正在进行中，请稍后再试.") { StatusCode = 400 };
        }

        var taskEntity = new WikiWebCrawleTaskEntity
        {
            WikiId = request.WikiId,
            WikiWebConfigId = request.WebConfigId,
            CrawleState = (int)CrawleState.None,
            MaxTokensPerParagraph = request.MaxTokensPerParagraph,
            OverlappingTokens = request.OverlappingTokens,
            Tokenizer = request.Tokenizer.ToString(),
            Message = "已提交"
        };

        _databaseContext.WikiWebCrawleTasks.Add(taskEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var message = new StartWebDocumentCrawleMessage
        {
            WikiId = request.WikiId,
            WebConfigId = request.WebConfigId,
            TaskId = taskEntity.Id
        };

        await _messagePublisher.AutoPublishAsync(message);

        return new SimpleGuid
        {
            Value = taskEntity.Id
        };
    }
}
