using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Models;

public class PluginKeyValue
{
    /// <summary>
    /// 插件类型.
    /// </summary>
    public PluginType PluginType { get; init; }

    /// <summary>
    /// 插件 id 或插件 key如果是自定义或原生插件，用 id，工具类用 key.
    /// </summary>
    public string PluginKey { get; init; }
}
