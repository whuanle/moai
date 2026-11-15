using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询当前接入可见的最近事件（REST 补发兜底）.
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

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsysEventsCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemId).NotEmpty().WithMessage("接入 id 不正确.");
        validate.RuleFor(x => x.Limit).InclusiveBetween(1, 500).WithMessage("limit 取值范围 1~500.");
    }
}
