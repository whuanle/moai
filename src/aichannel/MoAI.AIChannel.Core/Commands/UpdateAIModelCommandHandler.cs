using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAIModelCommand"/>
/// </summary>
public class UpdateAIModelCommandHandler : IRequestHandler<UpdateAIModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAIModelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAIModelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAIModelCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId && x.IsDeleted == 0, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        // 同一渠道下变更模型标识时需查重.
        if (model.ModelId != request.Meta.ModelId)
        {
            var exist = await _databaseContext.AiModels
                .AnyAsync(x => x.Id != request.ModelId && x.ChannelId == model.ChannelId && x.ModelId == request.Meta.ModelId && x.IsDeleted == 0, cancellationToken);

            if (exist)
            {
                throw new BusinessException("该渠道下已存在同名模型，请更换后重试.") { StatusCode = 409 };
            }
        }

        AIModelMetaMapper.Apply(model, request.Meta);
        model.Enabled = request.Enabled;

        _databaseContext.Update(model);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
