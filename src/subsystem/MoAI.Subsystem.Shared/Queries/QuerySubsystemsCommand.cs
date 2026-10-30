using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询子系统列表（仅 root），附带在线状态与授权团队数.
/// </summary>
public class QuerySubsystemsCommand : IRequest<QuerySubsystemsCommandResponse>, IModelValidator<QuerySubsystemsCommand>
{
    /// <summary>
    /// 按标识/名称模糊搜索，可选.
    /// </summary>
    public string? Search { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsystemsCommand> validate)
    {
        validate.RuleFor(x => x.Search).MaximumLength(50).WithMessage("搜索关键字最长 50 字符.");
    }
}
