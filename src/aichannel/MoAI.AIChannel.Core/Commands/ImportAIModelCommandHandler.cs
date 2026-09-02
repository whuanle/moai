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
/// <inheritdoc cref="ImportAIModelCommand"/>
/// </summary>
public class ImportAIModelCommandHandler : IRequestHandler<ImportAIModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportAIModelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public ImportAIModelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ImportAIModelCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return EmptyCommandResponse.Default;
        }

        var channelExist = await _databaseContext.AiChannels
            .AnyAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0, cancellationToken);

        if (!channelExist)
        {
            throw new BusinessException("未找到渠道，请检查渠道 id.") { StatusCode = 404 };
        }

        var existingModelIds = await _databaseContext.AiModels
            .Where(x => x.ChannelId == request.ChannelId && x.IsDeleted == 0)
            .Select(x => x.ModelId)
            .ToHashSetAsync(cancellationToken);

        // 保持只增不更：已存在未提供更新语义，保持不变；仅新增非重复项.
        foreach (var meta in request.Items)
        {
            if (string.IsNullOrWhiteSpace(meta.ModelId))
            {
                continue;
            }

            if (existingModelIds.Contains(meta.ModelId))
            {
                continue;
            }

            var model = new AiModelEntity
            {
                ChannelId = request.ChannelId,
                Enabled = true,
            };

            AIModelMetaMapper.Apply(model, meta);

            _databaseContext.AiModels.Add(model);
            existingModelIds.Add(meta.ModelId);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
