using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询子系统已授权团队 id 列表（仅 root），用于编辑回显.
/// </summary>
public class QuerySubsystemAuthorizedTeamsCommand : IRequest<QuerySubsystemAuthorizedTeamsCommandResponse>, IModelValidator<QuerySubsystemAuthorizedTeamsCommand>
{
    /// <summary>
    /// 子系统 id.
    /// </summary>
    public Guid SubsystemId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsystemAuthorizedTeamsCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemId).NotEmpty().WithMessage("子系统 id 不能为空.");
    }
}
