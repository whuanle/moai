using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Feishu.Models;

namespace MoAI.Wiki.Plugins.Feishu.Commands;

/// <summary>
/// 飞书配置.
/// </summary>
public class AddWikiFeishuConfigCommand : WikiFeishuConfig, IRequest<SimpleInt>, IModelValidator<AddWikiFeishuConfigCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddWikiFeishuConfigCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.AppSecret)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.SpaceId)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.ParentNodeToken)
            .NotEmpty().WithMessage("不能为空");
    }
}