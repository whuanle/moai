using FluentValidation;
using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 搜索知识库文档分片.
/// </summary>
public class SearchWikiDocumentTextCommand : IRequest<SearchWikiDocumentTextCommandResponse>, IModelValidator<SearchWikiDocumentTextCommand>
{
    /// <summary>
    ///  知识库 id.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 文档id，不设置时搜索整个知识库.
    /// </summary>
    public int? DocumentId { get; set; }

    /// <summary>
    /// 搜索文本，区配文本.
    /// </summary>
    public string? Query { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<SearchWikiDocumentTextCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
