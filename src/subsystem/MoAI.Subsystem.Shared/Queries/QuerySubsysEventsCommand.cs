using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询当前接入可见的最近事件（REST 补发兜底）；支持按事件名/前缀过滤，便于只拉取关心的域.
/// </summary>
public class QuerySubsysEventsCommand : IRequest<QuerySubsysEventsCommandResponse>, IModelValidator<QuerySubsysEventsCommand>
{
    /// <summary>接入 id，由 Controller 从调用方上下文回填.</summary>
    [JsonIgnore]
    public Guid SubsystemId { get; set; }

    /// <summary>上次收到的事件 id；空表示从当前窗口起点开始.</summary>
    public string? AfterId { get; init; }

    /// <summary>返回条数上限.</summary>
    public int Limit { get; init; } = 200;

    /// <summary>精确匹配的事件名（如 <c>subsys.user.created</c>），可选.</summary>
    public string? Event { get; init; }

    /// <summary>事件名前缀（如 <c>subsys.team.</c>），可选；与 <see cref="Event"/> 互斥.</summary>
    public string? EventPrefix { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsysEventsCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemId).NotEmpty().WithMessage("接入 id 不正确.");
        validate.RuleFor(x => x.Limit).InclusiveBetween(1, 500).WithMessage("limit 取值范围 1~500.");
        validate.RuleFor(x => x.Event)
            .MaximumLength(128).WithMessage("event 最长 128 字符.")
            .Must(x => string.IsNullOrWhiteSpace(x) || x.StartsWith("subsys.", StringComparison.Ordinal))
            .WithMessage("event 必须以 subsys. 开头.");
        validate.RuleFor(x => x.EventPrefix)
            .MaximumLength(64).WithMessage("eventPrefix 最长 64 字符.")
            .Must(x => string.IsNullOrWhiteSpace(x) || x.StartsWith("subsys.", StringComparison.Ordinal))
            .WithMessage("eventPrefix 必须以 subsys. 开头.");
        validate.RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.Event) || string.IsNullOrWhiteSpace(x.EventPrefix))
            .WithMessage("event 与 eventPrefix 不能同时指定.");
    }
}
