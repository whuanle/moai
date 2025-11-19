#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable SA1600 // Elements should be documented

using FluentValidation;
using Microsoft.SemanticKernel;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Models;
using MoAI.Plugin.Attributes;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.FeishuWebHookText;

public class FeishuWebHookTextPluginConfigValidation : AbstractValidator<FeishuWebHookTextPluginConfig>
{
    public FeishuWebHookTextPluginConfigValidation()
    {
        RuleFor(r => r.WebhookKey)
            .NotNull()
            .MinimumLength(10)
            .MaximumLength(100)
            .WithMessage("WebhookKey长度有误");
        RuleFor(r => r.SignKey)
            .MaximumLength(100)
            .WithMessage("sign_key长度有误");
    }
}