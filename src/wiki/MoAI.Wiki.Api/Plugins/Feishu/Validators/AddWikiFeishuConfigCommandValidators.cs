using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.Feishu.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Validators;

/// <summary>
/// AddWikiFeishuConfigCommandValidators.
/// </summary>
public class AddWikiFeishuConfigCommandValidators : Validator<AddWikiFeishuConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiFeishuConfigCommandValidators"/> class.
    /// </summary>
    public AddWikiFeishuConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        RuleFor(x => x.Config).NotNull();

        RuleFor(x => x.Config.AppId)
            .NotEmpty().WithMessage("不能为空");

        RuleFor(x => x.Config.AppSecret)
            .NotEmpty().WithMessage("不能为空");

        RuleFor(x => x.Config.SpaceId)
            .NotEmpty().WithMessage("不能为空");

        RuleFor(x => x.Config.ParentNodeToken)
            .NotEmpty().WithMessage("不能为空");
    }
}
