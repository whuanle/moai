using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Plugins.Feishu.Commands;

/// <summary>
/// 飞书配置.
/// </summary>
public class AddWikiFeishuConfigCommand : AddWikiPluginConfigCommand<WikiFeishuConfig>, IRequest<SimpleInt>, IModelValidator<AddWikiFeishuConfigCommand>
{
    /// <inheritdoc/>
    public void Validate(AbstractValidator<AddWikiFeishuConfigCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        validate.RuleFor(x => x.Config).NotNull();

        validate.RuleFor(x => x.Config.AppId)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.Config.AppSecret)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.Config.SpaceId)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.Config.ParentNodeToken)
            .NotEmpty().WithMessage("不能为空");
    }
}