using MediatR;

namespace MoAI.Plugin.InternalPluginCommands;

public class RunTestInternalPluginCommand : IRequest<RunTestInternalPluginCommandResponse>
{
    public int PluginId { get; init; }
    public string Params { get; init; }
}

public class RunTestInternalPluginCommandResponse
{
    /// <summary>
    /// 运行是否成功.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 响应结果.
    /// </summary>
    public string Response { get; init; }
}