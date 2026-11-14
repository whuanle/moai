using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询子系统组织授权配置（仅 root），用于编辑回显.
/// </summary>
public class QuerySubsystemOrgGrantsCommand : IRequest<QuerySubsystemOrgGrantsCommandResponse>, IModelValidator<QuerySubsystemOrgGrantsCommand>
{
    /// <summary>
    /// 子系统 id.
    /// </summary>
    public Guid SubsystemId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsystemOrgGrantsCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemId).NotEmpty().WithMessage("子系统 id 不能为空.");
    }
}
