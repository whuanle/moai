using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.WebDocuments.Commands;

namespace MoAI.Wiki.WebDocuments.Validators;

/// <summary>
/// AddWebDocumentConfigCommandValidators.
/// </summary>
public class AddWebDocumentConfigCommandValidators : Validator<AddWebDocumentConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddWebDocumentConfigCommandValidators"/> class.
    /// </summary>
    public AddWebDocumentConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        RuleFor(x => x.LimitMaxCount)
            .GreaterThan(0).WithMessage("最大抓取数量必须大于0")
            .LessThanOrEqualTo(1000).WithMessage("最大抓取数量不能超过1000");

        RuleFor(x => x.LimitAddress)
            .Must((a, b) =>
            {
                if (b != null)
                {
                    return a.Address.Host == b.Host;
                }

                return true;
            }).WithMessage("限制地址域名必须跟抓取地址一致");
    }
}
