using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAIChannelCommand"/>
/// </summary>
public class DeleteAIChannelCommandHandler : IRequestHandler<DeleteAIChannelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAIChannelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAIChannelCommand request, CancellationToken cancellationToken)
    {
        var channel = await _databaseContext.AiChannels
            .FirstOrDefaultAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0, cancellationToken);

        if (channel == null)
        {
            throw new BusinessException("未找到渠道，请检查 id 是否正确.") { StatusCode = 404 };
        }

        // 软删除渠道及其下的模型.
        channel.IsDeleted = 1;
        _databaseContext.Update(channel);

        var models = await _databaseContext.AiModels
            .Where(x => x.ChannelId == request.ChannelId && x.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        foreach (var model in models)
        {
            model.IsDeleted = 1;
            _databaseContext.Update(model);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
