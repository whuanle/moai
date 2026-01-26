using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Commands;
using MoAI.AiModel.Queries;
using MoAI.App.Chat.Works.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Chat.Works.Commands;

/// <summary>
/// <inheritdoc cref="CreateChatAppInstanceCommand"/>
/// </summary>
public class CreateChatAppInstanceCommandHandler : IRequestHandler<CreateChatAppInstanceCommand, CreateChatAppInstanceCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateChatAppInstanceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public CreateChatAppInstanceCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<CreateChatAppInstanceCommandResponse> Handle(CreateChatAppInstanceCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var title = "新对话";

        if (!string.IsNullOrEmpty(request.Title))
        {
            title = request.Title;
        }
        else if (string.IsNullOrEmpty(request.Title) && !string.IsNullOrEmpty(request.Question))
        {
            var appCommon = await _databaseContext.AppChatapps.Where(x => x.AppId == app.Id).FirstOrDefaultAsync();
            if (appCommon == null)
            {
                throw new BusinessException("应用不存在") { StatusCode = 404 };
            }

            var aiendpoint = await _mediator.Send(new QueryAiModelToAiEndpointCommand { AiModelId = appCommon.ModelId });
            var response = await _mediator.Send(new OneSimpleChatCommand
            {
                AiModelId = appCommon.ModelId,
                Channel = "app",
                ContextUserId = request.ContextUserId,
                ContextUserType = request.ContextUserType,
                Endpoint = aiendpoint,
                Prompt = "为以下内容生成一个简短的标题",
                Question = request.Question,
            });

            title = response.Content;
        }

        var chatEntity = new AppChatappChatEntity
        {
            Id = Guid.CreateVersion7(),
            AppId = request.AppId,
            Title = title,
            UserType = (int)request.ContextUserType,
        };

        await _databaseContext.AddAsync(chatEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateChatAppInstanceCommandResponse
        {
            ChatId = chatEntity.Id,
            Title = title
        };
    }
}
