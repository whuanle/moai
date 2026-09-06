using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAIModelAuthorizationCommand"/>
/// </summary>
public class UpdateAIModelAuthorizationCommandHandler : IRequestHandler<UpdateAIModelAuthorizationCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAIModelAuthorizationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAIModelAuthorizationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAIModelAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        if (model.IsPublic)
        {
            throw new BusinessException("公开模型对所有团队可用，无需设置团队授权.") { StatusCode = 400 };
        }

        var teamIds = request.TeamIds.Distinct().ToList();

        var existTeamIds = await _databaseContext.Teams
            .Where(x => teamIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var missingTeamIds = teamIds.Except(existTeamIds).ToList();
        if (missingTeamIds.Count > 0)
        {
            throw new BusinessException($"团队不存在：{string.Join(", ", missingTeamIds)}.") { StatusCode = 404 };
        }

        var currentTeamIds = await _databaseContext.AiModelAuthorizations
            .Where(x => x.AiModelId == request.ModelId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);

        foreach (var teamId in existTeamIds.Except(currentTeamIds))
        {
            _databaseContext.AiModelAuthorizations.Add(new AiModelAuthorizationEntity
            {
                AiModelId = request.ModelId,
                TeamId = teamId,
            });
        }

        var removedTeamIds = currentTeamIds.Except(existTeamIds).ToList();
        if (removedTeamIds.Count > 0)
        {
            // 取消授权的同时移除该团队的额度规则与余额，未授权团队不应残留额度.
            var removeAuthorizations = await _databaseContext.AiModelAuthorizations
                .Where(x => x.AiModelId == request.ModelId && removedTeamIds.Contains(x.TeamId))
                .ToListAsync(cancellationToken);
            foreach (var authorization in removeAuthorizations)
            {
                authorization.IsDeleted = 1;
                _databaseContext.Update(authorization);
            }

            var removeLimits = await _databaseContext.AiModelLimits
                .Where(x => x.ModelId == request.ModelId && removedTeamIds.Contains(x.TeamId))
                .ToListAsync(cancellationToken);
            var removeLimitIds = removeLimits.Select(x => x.Id).ToList();
            foreach (var limit in removeLimits)
            {
                limit.IsDeleted = 1;
                _databaseContext.Update(limit);
            }

            if (removeLimitIds.Count > 0)
            {
                var removeQuotas = await _databaseContext.AiModelQuota
                    .Where(x => removeLimitIds.Contains(x.LimitId))
                    .ToListAsync(cancellationToken);
                foreach (var quota in removeQuotas)
                {
                    quota.IsDeleted = 1;
                    _databaseContext.Update(quota);
                }
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
