using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Commands;
using MoAI.AiModel.Queries;
using MoAI.App.Apps.CommonApp.Handlers;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.App.AppCommon.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAppChatCommand"/>
/// </summary>
public class CreateAppChatCommandHandler : IRequestHandler<CreateAppChatCommand, CreateAppChatCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAppChatCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public CreateAppChatCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<CreateAppChatCommandResponse> Handle(CreateAppChatCommand request, CancellationToken cancellationToken)
    {
        // 检查应用是否存在且用户有权访问
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
            var appCommon = await _databaseContext.AppCommons.Where(x => x.AppId == app.Id).FirstOrDefaultAsync();
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
                Prompt = "为以下内容生成一个简短的标题，标题不超过20个字",
                Question = request.Question,
            });

            title = response.Content;
        }

        var chatEntity = new AppCommonChatEntity
        {
            Id = Guid.CreateVersion7(),
            AppId = request.AppId,
            Title = title,
            UserType = (int)request.ContextUserType,
        };

        await _databaseContext.AppCommonChats.AddAsync(chatEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateAppChatCommandResponse
        {
            ChatId = chatEntity.Id,
            Title = title
        };
    }
}
