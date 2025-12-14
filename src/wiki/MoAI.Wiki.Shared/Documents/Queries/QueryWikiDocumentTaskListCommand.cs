using FluentValidation;
using MediatR;
using MoAI.Wiki.Documents.Queries.Responses;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询文档任务列表.
/// </summary>
public class QueryWikiDocumentTaskListCommand : IRequest<IReadOnlyCollection<WikiDocumentEmbeddingTaskItem>>, IModelValidator<QueryWikiDocumentTaskListCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<QueryWikiDocumentTaskListCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("文档id不正确")
            .GreaterThan(0).WithMessage("文档id不正确");
    }
}
