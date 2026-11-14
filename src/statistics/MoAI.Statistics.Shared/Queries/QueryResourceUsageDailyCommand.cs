using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Queries;

/// <summary>
/// 分页查询资源使用统计（每日），仅管理员.
/// </summary>
public class QueryResourceUsageDailyCommand : PagedParamter, IRequest<QueryResourceUsageDailyCommandResponse>, IModelValidator<QueryResourceUsageDailyCommand>
{
    /// <summary>
    /// 资源类型名，为空则不过滤（见 <see cref="MoAI.Statistics.Services.ResourceUsageNames"/>）.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 起始统计日期（含），为空则不限.
    /// </summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>
    /// 结束统计日期（含），为空则不限.
    /// </summary>
    public DateOnly? EndDate { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryResourceUsageDailyCommand> validate)
    {
        validate.RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate ?? DateOnly.MaxValue)
            .When(x => x.StartDate != null && x.EndDate != null)
            .WithMessage("起始日期不能晚于结束日期");
    }
}
