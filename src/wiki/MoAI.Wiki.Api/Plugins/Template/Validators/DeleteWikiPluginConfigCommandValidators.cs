using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Plugins.Template.Validators;

/// <summary>
/// DeleteWikiPluginConfigCommandValidators.
/// </summary>
public class DeleteWikiPluginConfigCommandValidators : AbstractValidator<DeleteWikiPluginConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiPluginConfigCommandValidators"/> class.
    /// </summary>
    public DeleteWikiPluginConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
