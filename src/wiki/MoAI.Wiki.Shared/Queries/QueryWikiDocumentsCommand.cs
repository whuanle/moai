using FluentValidation;
using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询知识库的文档列表（不含正文），仅团队成员可访问.
/// </summary>
public class QueryWikiDocumentsCommand : IRequest<QueryWikiDocumentsCommandResponse>, IModelValidator<QueryWikiDocumentsCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiDocumentsCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库 id 不正确.");
    }
}
