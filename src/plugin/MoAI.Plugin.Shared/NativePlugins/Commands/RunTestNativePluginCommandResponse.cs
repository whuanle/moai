using MediatR;

namespace MoAI.Plugin.NativePlugins.Commands;

public class RunTestNativePluginCommandResponse
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