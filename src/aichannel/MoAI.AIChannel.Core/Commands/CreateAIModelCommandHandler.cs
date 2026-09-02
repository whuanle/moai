using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAIModelCommand"/>
/// </summary>
public class CreateAIModelCommandHandler : IRequestHandler<CreateAIModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAIModelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateAIModelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CreateAIModelCommand request, CancellationToken cancellationToken)
    {
        var channelExist = await _databaseContext.AiChannels
            .AnyAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0, cancellationToken);

        if (!channelExist)
        {
            throw new BusinessException("未找到渠道，请检查渠道 id.") { StatusCode = 404 };
        }

        var exist = await _databaseContext.AiModels
            .AnyAsync(x => x.ChannelId == request.ChannelId && x.ModelId == request.Meta.ModelId && x.IsDeleted == 0, cancellationToken);

        if (exist)
        {
            throw new BusinessException("该渠道下已存在同名模型，请更换后重试.") { StatusCode = 409 };
        }

        var model = new AiModelEntity
        {
            ChannelId = request.ChannelId,
            Enabled = request.Enabled,
        };

        AIModelMetaMapper.Apply(model, request.Meta);

        _databaseContext.AiModels.Add(model);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
