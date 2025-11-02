using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.WebDocuments.Commands;

namespace MoAI.Wiki.WebDocuments.Validators;

/// <summary>
/// StartWebDocumentCrawleCommandValidators.
/// </summary>
public class StartWebDocumentCrawleCommandValidators : Validator<StartWebDocumentCrawleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartWebDocumentCrawleCommandValidators"/> class.
    /// </summary>
    public StartWebDocumentCrawleCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.WebConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
