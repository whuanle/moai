namespace MoAI.AIPlugin.Services;

/// <summary>
/// OpenApi 文档解析结果.
/// </summary>
internal sealed class OpenApiParseResult
{
    /// <summary>
    /// 服务器地址.
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// 接口列表.
    /// </summary>
    public IReadOnlyCollection<OpenApiFunctionInfo> Functions { get; set; } = new List<OpenApiFunctionInfo>();
}
