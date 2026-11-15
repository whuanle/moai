using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询当前系统级接入自身的授权快照（团队范围/授权团队/组织授权）.
/// </summary>
public class QuerySubsysGrantsCommand : IRequest<Responses.QuerySubsysGrantsCommandResponse>, IModelValidator<QuerySubsysGrantsCommand>
{
    /// <summary>接入 id，由 Controller 从调用方上下文回填.</summary>
    [JsonIgnore]
    public Guid SubsystemId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsysGrantsCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemId).NotEmpty().WithMessage("接入 id 不正确.");
    }
}
