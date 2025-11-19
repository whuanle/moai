using MediatR;

namespace MoAI.Plugin.NativePlugins.Commands;

public class RunTestNativePluginCommand : IRequest<RunTestNativePluginCommandResponse>
{
    /// <summary>
    /// 插件模板.
    /// </summary>
    public string TemplatePluginKey { get; init; } = string.Empty;

    /// <summary>
    /// 实例化的插件 id，如果是工具插件，则不需要填写.
    /// </summary>
    public int? PluginId { get; init; }

    /// <summary>
    /// 运行参数.
    /// </summary>
    public string Params { get; init; }
}
