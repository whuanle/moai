using System;
using FluentValidation;
using MediatR;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Queries;

/// <summary>
/// 按日聚合查询资源使用趋势（数据分析），仅管理员；区间内无数据的日期补 0.
/// </summary>
public class QueryResourceUsageTrendCommand : IRequest<QueryResourceUsageTrendCommandResponse>, IModelValidator<QueryResourceUsageTrendCommand>
{
    /// <summary>
    /// 资源类型名，为空则统计全部资源类型合计（见 <see cref="MoAI.Statistics.Services.ResourceUsageNames"/>）.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 起始统计日期（含），为空默认近 30 天（含今天，UTC）.
    /// </summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>
    /// 结束统计日期（含），为空默认今天（UTC）.
    /// </summary>
    public DateOnly? EndDate { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryResourceUsageTrendCommand> validate)
    {
        validate.RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate ?? DateOnly.MaxValue)
            .When(x => x.StartDate != null && x.EndDate != null)
            .WithMessage("起始日期不能晚于结束日期");
    }
}
