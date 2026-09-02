using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAIChannelCommand"/>
/// </summary>
public class UpdateAIChannelCommandHandler : IRequestHandler<UpdateAIChannelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAIChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAIChannelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAIChannelCommand request, CancellationToken cancellationToken)
    {
        var channel = await _databaseContext.AiChannels
            .FirstOrDefaultAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0, cancellationToken);

        if (channel == null)
        {
            throw new BusinessException("未找到渠道，请检查 id 是否正确.") { StatusCode = 404 };
        }

        if (channel.Name != request.Name)
        {
            var exist = await _databaseContext.AiChannels
                .AnyAsync(x => x.Id != request.ChannelId && x.Name == request.Name && x.IsDeleted == 0, cancellationToken);

            if (exist)
            {
                throw new BusinessException("渠道名称已存在，请更换后重试.") { StatusCode = 409 };
            }
        }

        channel.ProviderKey = request.ProviderKey;
        channel.Name = request.Name;
        channel.ProtocolFamily = (int)request.ProtocolFamily;
        channel.BaseUrl = request.BaseUrl ?? channel.BaseUrl;
        channel.Enabled = request.Enabled;
        channel.Description = request.Description;

        if (!string.IsNullOrEmpty(request.ApiKey))
        {
            channel.ApiKey = request.ApiKey;
        }

        _databaseContext.Update(channel);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
