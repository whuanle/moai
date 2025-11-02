using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.WebDocuments.Commands;

namespace MoAI.Wiki.WebDocuments.Validators;

/// <summary>
/// DeleteWebDocumentConfigCommandValidators.
/// </summary>
public class DeleteWebDocumentConfigCommandValidators : Validator<DeleteWebDocumentConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWebDocumentConfigCommandValidators"/> class.
    /// </summary>
    public DeleteWebDocumentConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.WikiWebConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
