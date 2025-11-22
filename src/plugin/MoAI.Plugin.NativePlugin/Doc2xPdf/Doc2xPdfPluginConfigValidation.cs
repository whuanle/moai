#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable SA1600 // Elements should be documented

using FluentValidation;

namespace MoAI.Plugin.Plugins.Doc2xPdf;

public class Doc2xPdfPluginConfigValidation : AbstractValidator<Doc2xPdfPluginConfig>
{
    public Doc2xPdfPluginConfigValidation()
    {
        RuleFor(r => r.Key)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100)
            .WithMessage("WebhookKey长度有误");
    }
}