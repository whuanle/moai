namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 单工具参数运行示例查询响应.
/// </summary>
public class QueryPluginToolExampleCommandResponse
{
    /// <summary>
    /// 工具 key.
    /// </summary>
    public string ToolKey { get; init; } = string.Empty;

    /// <summary>
    /// 参数运行示例 JSON 字符串，可为空.
    /// </summary>
    public string? ParamsExample { get; init; }
}
