using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAIModelQuotaCommand"/>
/// </summary>
public class UpdateAIModelQuotaCommandHandler : IRequestHandler<UpdateAIModelQuotaCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAIModelQuotaCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAIModelQuotaCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAIModelQuotaCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        if (model.IsPublic)
        {
            if (request.TeamId != 0)
            {
                throw new BusinessException("公开模型仅支持设置所有团队共享的全局额度（teamId=0）.") { StatusCode = 400 };
            }
        }
        else
        {
            if (request.TeamId == 0)
            {
                throw new BusinessException("私有模型额度必须指定授权团队.") { StatusCode = 400 };
            }

            var authorized = await _databaseContext.AiModelAuthorizations
                .AnyAsync(x => x.AiModelId == request.ModelId && x.TeamId == request.TeamId, cancellationToken);

            if (!authorized)
            {
                throw new BusinessException("该团队未被授权使用此模型，请先授权.") { StatusCode = 400 };
            }
        }

        var limit = await _databaseContext.AiModelLimits
            .FirstOrDefaultAsync(x => x.ModelId == request.ModelId && x.TeamId == request.TeamId, cancellationToken);

        if (limit == null)
        {
            limit = new AiModelLimitEntity
            {
                ModelId = request.ModelId,
                TeamId = request.TeamId,
                PeriodUnit = request.PeriodUnit,
                PeriodValue = request.PeriodValue,
                LimitValue = request.LimitValue,
            };

            _databaseContext.AiModelLimits.Add(limit);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            limit.PeriodUnit = request.PeriodUnit;
            limit.PeriodValue = request.PeriodValue;
            limit.LimitValue = request.LimitValue;
            _databaseContext.AiModelLimits.Update(limit);
        }

        await UpsertQuotaBalanceAsync(limit, request.LimitValue, cancellationToken);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task UpsertQuotaBalanceAsync(AiModelLimitEntity limit, long limitValue, CancellationToken cancellationToken)
    {
        var quota = await _databaseContext.AiModelQuota
            .FirstOrDefaultAsync(x => x.LimitId == limit.Id, cancellationToken);

        if (quota == null)
        {
            var now = DateTimeOffset.UtcNow;
            _databaseContext.AiModelQuota.Add(new AiModelQuotumEntity
            {
                LimitId = limit.Id,
                ModelId = limit.ModelId,
                TeamId = limit.TeamId,
                TotalLimit = limitValue,
                UsedTokens = 0,
                PeriodStart = now,
                PeriodEnd = ComputePeriodEnd(now, limit.PeriodUnit, limit.PeriodValue),
            });
            return;
        }

        // 快照总额度；重置周期发生变化时重新开启一个周期并清零已消耗.
        quota.TotalLimit = limitValue;
        var periodChanged = quota.PeriodEnd != ComputePeriodEnd(quota.PeriodStart, limit.PeriodUnit, limit.PeriodValue);
        if (periodChanged)
        {
            var now = DateTimeOffset.UtcNow;
            quota.PeriodStart = now;
            quota.PeriodEnd = ComputePeriodEnd(now, limit.PeriodUnit, limit.PeriodValue);
            quota.UsedTokens = 0;
        }

        _databaseContext.AiModelQuota.Update(quota);
    }

    private static DateTimeOffset ComputePeriodEnd(DateTimeOffset periodStart, int periodUnit, int periodValue)
    {
        return periodUnit switch
        {
            1 => periodStart.AddHours(periodValue),
            2 => periodStart.AddDays(periodValue),
            3 => periodStart.AddDays(7L * periodValue),
            4 => periodStart.AddMonths(periodValue),
            _ => DateTimeOffset.MaxValue,
        };
    }
}
