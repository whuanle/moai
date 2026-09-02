using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAIChannelCommand"/>
/// </summary>
public class CreateAIChannelCommandHandler : IRequestHandler<CreateAIChannelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAIChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateAIChannelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CreateAIChannelCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.AiChannels
            .AnyAsync(x => x.Name == request.Name && x.IsDeleted == 0, cancellationToken);

        if (exist)
        {
            throw new BusinessException("渠道名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var channel = new AiChannelEntity
        {
            ProviderKey = request.ProviderKey,
            Name = request.Name,
            ProtocolFamily = (int)request.ProtocolFamily,
            BaseUrl = request.BaseUrl ?? string.Empty,
            ApiKey = request.ApiKey ?? string.Empty,
            Enabled = request.Enabled,
            Description = request.Description,
        };

        _databaseContext.AiChannels.Add(channel);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
