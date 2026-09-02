using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.AIChannel.Models;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="SyncAIModelCommand"/>
/// </summary>
public class SyncAIModelCommandHandler : IRequestHandler<SyncAIModelCommand, SyncAIModelCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly AIModelProviderService _providerService;
    private readonly AIModelCatalogService _catalogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAIModelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="providerService"></param>
    /// <param name="catalogService"></param>
    public SyncAIModelCommandHandler(
        DatabaseContext databaseContext,
        AIModelProviderService providerService,
        AIModelCatalogService catalogService)
    {
        _databaseContext = databaseContext;
        _providerService = providerService;
        _catalogService = catalogService;
    }

    /// <inheritdoc/>
    public async Task<SyncAIModelCommandResponse> Handle(SyncAIModelCommand request, CancellationToken cancellationToken)
    {
        var channel = await _databaseContext.AiChannels
            .FirstOrDefaultAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0, cancellationToken);

        if (channel == null)
        {
            throw new BusinessException("未找到渠道，请检查 id 是否正确.") { StatusCode = 404 };
        }

        var protocol = (AIProtocolFamily)channel.ProtocolFamily;
        var modelIds = await _providerService.GetModelIdsAsync(protocol, channel.BaseUrl, channel.ApiKey, cancellationToken);

        var existing = await _databaseContext.AiModels
            .Where(x => x.ChannelId == channel.Id && x.IsDeleted == 0)
            .ToDictionaryAsync(x => x.ModelId, x => x, cancellationToken);

        var added = 0;
        foreach (var modelId in modelIds)
        {
            var meta = await _catalogService.GetModelAsync(channel.ProviderKey, modelId, cancellationToken);
            var shouldEnable = meta != null;

            // 匹配到 models.json 的模型默认启用；未匹配到（无参数）默认禁用.
            if (existing.TryGetValue(modelId, out var existedModel))
            {
                var derivedKind = meta != null
                    ? AIModelMetaMapper.DeriveModelKind(meta)
                    : AIModelMetaMapper.DeriveModelKind(new AIChannelModelMeta { ModelId = modelId, Name = modelId });

                if (existedModel.Enabled != shouldEnable || existedModel.ModelKind != derivedKind)
                {
                    existedModel.Enabled = shouldEnable;
                    existedModel.ModelKind = derivedKind;
                    _databaseContext.Update(existedModel);
                }

                continue;
            }

            var model = new AiModelEntity
            {
                ChannelId = channel.Id,
                Enabled = shouldEnable,
                ModelId = modelId,
                Name = modelId,
                ModelKind = AIModelMetaMapper.DeriveModelKind(new AIChannelModelMeta { ModelId = modelId, Name = modelId }),
            };

            if (meta != null)
            {
                AIModelMetaMapper.Apply(model, meta);
            }

            _databaseContext.AiModels.Add(model);
            existing[modelId] = model;
            added++;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SyncAIModelCommandResponse
        {
            Total = modelIds.Count,
            Added = added,
            Skipped = modelIds.Count - added,
        };
    }
}
