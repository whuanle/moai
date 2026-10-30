using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAIChannelAuthorizationCommand"/>
/// </summary>
public class UpdateAIChannelAuthorizationCommandHandler : IRequestHandler<UpdateAIChannelAuthorizationCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAIChannelAuthorizationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAIChannelAuthorizationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAIChannelAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var channel = await _databaseContext.AiChannels
            .FirstOrDefaultAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0, cancellationToken);

        if (channel == null)
        {
            throw new BusinessException("未找到渠道，请检查 id 是否正确.") { StatusCode = 404 };
        }

        if (channel.TeamId > 0)
        {
            throw new BusinessException("团队自有渠道模型仅本团队可用，无渠道授权概念.") { StatusCode = 400 };
        }

        var grantTeamIds = request.GrantTeamIds.Distinct().ToList();
        var revokeTeamIds = request.RevokeTeamIds.Except(grantTeamIds).Distinct().ToList();

        var teamIds = grantTeamIds.Concat(revokeTeamIds).ToList();
        var existTeamIds = await _databaseContext.Teams
            .Where(x => teamIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var missingTeamIds = teamIds.Except(existTeamIds).ToList();
        if (missingTeamIds.Count > 0)
        {
            throw new BusinessException($"团队不存在：{string.Join(", ", missingTeamIds)}.") { StatusCode = 404 };
        }

        // 公开模型不参与渠道级授权（保持公开），仅处理私有模型.
        var privateModelIds = await _databaseContext.AiModels
            .Where(x => x.ChannelId == request.ChannelId && x.IsDeleted == 0 && !x.IsPublic)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (privateModelIds.Count == 0)
        {
            return EmptyCommandResponse.Default;
        }

        var currentAuthorizations = await _databaseContext.AiModelAuthorizations
            .Where(x => privateModelIds.Contains(x.AiModelId) && x.IsDeleted == 0)
            .ToListAsync(cancellationToken);
        var currentPairs = currentAuthorizations
            .Select(x => (x.AiModelId, x.TeamId))
            .ToHashSet();

        // 取消授权：存在的移除，未授权的跳过（幂等）.
        foreach (var authorization in currentAuthorizations.Where(x => revokeTeamIds.Contains(x.TeamId)))
        {
            authorization.IsDeleted = 1;
            _databaseContext.Update(authorization);
        }

        // 补齐授权：缺失的新增，已授权的跳过（幂等）；软删行不占用过滤唯一索引，直接新增.
        foreach (var modelId in privateModelIds)
        {
            foreach (var teamId in grantTeamIds)
            {
                if (currentPairs.Contains((modelId, teamId)))
                {
                    continue;
                }

                _databaseContext.AiModelAuthorizations.Add(new AiModelAuthorizationEntity
                {
                    AiModelId = modelId,
                    TeamId = teamId,
                });
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
