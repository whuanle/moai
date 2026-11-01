using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 分页查询登录日志，仅管理员.
/// </summary>
public class QueryLoginLogCommand : PagedParamter, IRequest<QueryLoginLogCommandResponse>, IModelValidator<QueryLoginLogCommand>
{
    /// <summary>
    /// 用户名（登录尝试值，模糊匹配），为空则不过滤.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// 来源 IP（精确匹配），为空则不过滤.
    /// </summary>
    public string? Ip { get; init; }

    /// <summary>
    /// 是否登录成功，为空则不过滤.
    /// </summary>
    public bool? IsSuccess { get; init; }

    /// <summary>
    /// 风险等级（0~3），为空则不过滤.
    /// </summary>
    public int? RiskLevel { get; init; }

    /// <summary>
    /// 起始时间（含），为空则不限.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// 结束时间（含），为空则不限.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryLoginLogCommand> validate)
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
