using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.AIChannel.Models;
using MoAI.AIChannel.Services;
using MoAI.Database;
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
            .FirstOrDefaultAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0 && x.TeamId == request.TeamId, cancellationToken);

        if (channel == null)
        {
            throw new BusinessException("未找到渠道，请检查 id 是否正确.") { StatusCode = 404 };
        }

        var protocol = (AIProtocolFamily)channel.ProtocolFamily;
        var modelIds = await _providerService.GetModelIdsAsync(protocol, channel.BaseUrl, channel.ApiKey, cancellationToken);

        var existingModelIds = await _databaseContext.AiModels
            .Where(x => x.ChannelId == channel.Id && x.IsDeleted == 0)
            .Select(x => x.ModelId)
            .ToHashSetAsync(cancellationToken);

        // 只探测不落库：候选列表返回给前端，由用户挑选后走 import 接口导入.
        var items = new List<SyncAIModelCandidateItem>(modelIds.Count);
        foreach (var modelId in modelIds)
        {
            var meta = await _catalogService.GetModelAsync(channel.ProviderKey, modelId, cancellationToken);
            items.Add(new SyncAIModelCandidateItem
            {
                Meta = meta ?? new AIChannelModelMeta { ModelId = modelId, Name = modelId },
                Recognized = meta != null,
                Exists = existingModelIds.Contains(modelId),
                ModelKind = AIModelMetaMapper.ResolveModelKind(
                    meta ?? new AIChannelModelMeta { ModelId = modelId, Name = modelId }),
            });
        }

        return new SyncAIModelCommandResponse
        {
            Total = modelIds.Count,
            Items = items,
        };
    }
}
