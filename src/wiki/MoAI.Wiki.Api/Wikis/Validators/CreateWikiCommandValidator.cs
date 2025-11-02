using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Validators;

/// <summary>
/// CreateWikiCommandValidator
/// </summary>
public class CreateWikiCommandValidator : Validator<CreateWikiCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiCommandValidator"/> class.
    /// </summary>
    public CreateWikiCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("知识库名称长度在 2-20 之间.")
            .Length(2, 20).WithMessage("知识库名称长度在 2-20 之间.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("知识库描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("知识库描述长度在 2-255 之间.");
    }
}
