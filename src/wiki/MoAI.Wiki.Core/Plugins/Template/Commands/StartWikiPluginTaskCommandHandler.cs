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

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// <inheritdoc cref="StartWikiPluginTaskCommand"/>
/// </summary>
public class StartWikiPluginTaskCommandHandler : IRequestHandler<StartWikiPluginTaskCommand, SimpleGuid>
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiPluginTaskCommandHandler"/> class.
    /// </summary>
    /// <param name="messagePublisher"></param>
    /// <param name="databaseContext"></param>
    public StartWikiPluginTaskCommandHandler(IMessagePublisher messagePublisher, DatabaseContext databaseContext)
    {
        _messagePublisher = messagePublisher;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(StartWikiPluginTaskCommand request, CancellationToken cancellationToken)
    {
        var existTask = await _databaseContext.WorkerTasks.AnyAsync(x => x.BindType == "wiki" && x.BindId == request.ConfigId && x.State < (int)WorkerState.Cancal, cancellationToken);

        if (existTask == true)
        {
            throw new BusinessException("当前知识库网页爬取任务正在进行中，请稍后再试.") { StatusCode = 400 };
        }

        var taskEntity = new WorkerTaskEntity
        {
            BindType = "wiki",
            BindId = request.ConfigId,
            State = (int)WorkerState.Wait,
            Data = request.ToJsonString(),
            Message = "已提交"
        };

        _databaseContext.WorkerTasks.Add(taskEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // todo：后期改
        var message = new StartWikiFeishuMessage
        {
            WikiId = request.WikiId,
            ConfigId = request.ConfigId,
            TaskId = taskEntity.Id
        };

        await _messagePublisher.AutoPublishAsync(message);

        return new SimpleGuid
        {
            Value = taskEntity.Id
        };
    }
}
