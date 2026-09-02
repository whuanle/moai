namespace MoAI.AiPlugin.Models;

/// <summary>
/// 插件执行结果。响应数据以 JSON 字符串形式承载，便于序列化任意类型.
/// </summary>
public class PluginRunResult
{
    /// <summary>
    /// 插件 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 是否执行成功.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 执行失败时的错误信息.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 执行成功时的响应结果 JSON 字符串.
    /// </summary>
    public string? DataJson { get; init; }

    /// <summary>
    /// 响应结果类型的完整名称.
    /// </summary>
    public string? ResponseType { get; init; }
}
