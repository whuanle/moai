using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 分页查询登录风险事件，仅管理员.
/// </summary>
public class QueryRiskEventListCommand : PagedParamter, IRequest<QueryRiskEventListCommandResponse>, IModelValidator<QueryRiskEventListCommand>
{
    /// <summary>
    /// 是否已读，为空则不过滤.
    /// </summary>
    public bool? IsRead { get; init; }

    /// <summary>
    /// 风险等级（2=中，3=高），为空则不过滤.
    /// </summary>
    public int? RiskLevel { get; init; }

    /// <summary>
    /// 用户名（模糊匹配），为空则不过滤.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// 起始时间（含），为空则不限.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// 结束时间（含），为空则不限.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryRiskEventListCommand> validate)
    {
        validate.RuleFor(x => x.RiskLevel)
            .InclusiveBetween(0, 3)
            .When(x => x.RiskLevel != null)
            .WithMessage("风险等级必须是 0~3 之间的整数");
        validate.RuleFor(x => x.StartTime)
            .LessThanOrEqualTo(x => x.EndTime ?? DateTimeOffset.MaxValue)
            .When(x => x.StartTime != null && x.EndTime != null)
            .WithMessage("起始时间不能晚于结束时间");
    }
}
