using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAIModelQuotaCommand"/>
/// </summary>
public class DeleteAIModelQuotaCommandHandler : IRequestHandler<DeleteAIModelQuotaCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIModelQuotaCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAIModelQuotaCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAIModelQuotaCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        if (model.IsPublic && request.TeamId != 0)
        {
            throw new BusinessException("公开模型仅支持移除全局额度（teamId=0）.") { StatusCode = 400 };
        }

        if (!model.IsPublic && request.TeamId == 0)
        {
            throw new BusinessException("私有模型额度必须指定授权团队.") { StatusCode = 400 };
        }

        var limit = await _databaseContext.AiModelLimits
            .FirstOrDefaultAsync(x => x.ModelId == request.ModelId && x.TeamId == request.TeamId, cancellationToken);

        if (limit == null)
        {
            throw new BusinessException("未找到该主体的额度规则.") { StatusCode = 404 };
        }

        limit.IsDeleted = 1;
        _databaseContext.AiModelLimits.Update(limit);

        var quotas = await _databaseContext.AiModelQuota
            .Where(x => x.LimitId == limit.Id)
            .ToListAsync(cancellationToken);
        foreach (var quota in quotas)
        {
            quota.IsDeleted = 1;
            _databaseContext.AiModelQuota.Update(quota);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
