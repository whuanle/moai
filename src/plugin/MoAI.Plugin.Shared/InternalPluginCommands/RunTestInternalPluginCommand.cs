using MediatR;

namespace MoAI.Plugin.InternalPluginCommands;

public class RunTestInternalPluginCommand : IRequest<RunTestInternalPluginCommandResponse>
{
    /// <summary>
    /// 实例化的插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 运行参数.
    /// </summary>
    public string Params { get; init; }
}
