using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Documents.Commands;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// EmbeddingDocumentCommandValidator.
/// </summary>
public class EmbeddingDocumentCommandValidator : Validator<EmbeddingDocumentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentCommandValidator"/> class.
    /// </summary>
    public EmbeddingDocumentCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("文档id不正确")
            .GreaterThan(0).WithMessage("文档id不正确");
    }
}
