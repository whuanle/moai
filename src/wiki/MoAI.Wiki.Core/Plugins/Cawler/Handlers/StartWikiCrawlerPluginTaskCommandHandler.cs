using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Handlers;

/// <summary>
/// <inheritdoc cref="StartWikiCrawlerPluginTaskCommand"/>
/// </summary>
public class StartWikiCrawlerPluginTaskCommandHandler : IRequestHandler<StartWikiCrawlerPluginTaskCommand, EmptyCommandResponse>
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiCrawlerPluginTaskCommandHandler"/> class.
    /// </summary>
    /// <param name="messagePublisher"></param>
    /// <param name="databaseContext"></param>
    public StartWikiCrawlerPluginTaskCommandHandler(IMessagePublisher messagePublisher, DatabaseContext databaseContext)
    {
        _messagePublisher = messagePublisher;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(StartWikiCrawlerPluginTaskCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WikiPluginConfigs.FirstOrDefaultAsync(x => x.Id == request.ConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("配置不存在") { StatusCode = 404 };
        }

        if (request.IsStart == true)
        {
            if (entity.WorkState == (int)WorkerState.Processing || entity.WorkState == (int)WorkerState.Wait)
            {
                throw new BusinessException("当前已有任务在运行中") { StatusCode = 400 };
            }

            entity.WorkState = (int)WorkerState.Wait;
            _databaseContext.WikiPluginConfigs.Update(entity);
            await _databaseContext.SaveChangesAsync(cancellationToken);

            var message = new StartWikiCrawlerMessage
            {
                WikiId = request.WikiId,
                ConfigId = request.ConfigId,
            };

            await _messagePublisher.AutoPublishAsync(message);
        }
        else
        {
            if (entity.WorkState < (int)WorkerState.Cancal)
            {
                throw new BusinessException("当前没有运行中的任务") { StatusCode = 400 };
            }

            entity.WorkState = (int)WorkerState.Cancal;
            _databaseContext.WikiPluginConfigs.Update(entity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
