namespace MoAI.AIPlugin.Services;

/// <summary>
/// OpenApi 接口信息.
/// </summary>
internal sealed class OpenApiFunctionInfo
{
    /// <summary>
    /// 接口名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// api 路径.
    /// </summary>
    public string Path { get; set; } = default!;
}
