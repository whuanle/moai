using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAIModelVisibilityCommand"/>
/// </summary>
public class UpdateAIModelVisibilityCommandHandler : IRequestHandler<UpdateAIModelVisibilityCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAIModelVisibilityCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAIModelVisibilityCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAIModelVisibilityCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        var isTeamChannel = await _databaseContext.AiChannels
            .AnyAsync(x => x.Id == model.ChannelId && x.TeamId > 0, cancellationToken);
        if (isTeamChannel)
        {
            throw new BusinessException("团队自有渠道模型不支持设置公私有可见性，仅本团队可用.") { StatusCode = 400 };
        }

        model.IsPublic = request.IsPublic;
        _databaseContext.AiModels.Update(model);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
