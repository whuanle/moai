using FluentValidation;
using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询团队下的知识库列表，仅团队成员可访问.
/// </summary>
public class QueryWikisCommand : IRequest<QueryWikisCommandResponse>, IModelValidator<QueryWikisCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikisCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
