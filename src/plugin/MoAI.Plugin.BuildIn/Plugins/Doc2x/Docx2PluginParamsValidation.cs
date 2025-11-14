#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using FluentValidation;
using MoAI.Plugin.Plugins.FeishuWebHookText;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.Doc2;

public class Docx2PluginParamsValidation : AbstractValidator<Docx2PluginParams>
{
    public Docx2PluginParamsValidation()
    {
        RuleFor(r => r.Key)
            .NotEmpty();
    }
}