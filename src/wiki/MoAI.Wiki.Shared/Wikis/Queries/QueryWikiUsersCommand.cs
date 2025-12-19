using FluentValidation;
using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询知识库协作成员.
/// </summary>
public class QueryWikiUsersCommand : IRequest<QueryWikiUsersCommandResponse>, IModelValidator<QueryWikiUsersCommand>
{
    /// <summary>
    /// 查询知识库协作的成员列表.
    /// </summary>
    public int WikiId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiUsersCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
