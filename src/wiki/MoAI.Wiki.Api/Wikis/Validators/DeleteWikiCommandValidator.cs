using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Validators;

/// <summary>
/// DeleteWikiCommandValidator
/// </summary>
public class DeleteWikiCommandValidator : AbstractValidator<DeleteWikiCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiCommandValidator"/> class.
    /// </summary>
    public DeleteWikiCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
