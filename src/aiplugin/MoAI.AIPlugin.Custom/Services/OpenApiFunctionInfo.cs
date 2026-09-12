using System.Collections.Generic;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// OpenApi 接口信息.
/// </summary>
public sealed class OpenApiFunctionInfo
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

/// <summary>
/// OpenApi 接口参数信息.
/// </summary>
public sealed class OpenApiParameterInfo
{
    /// <summary>
    /// 参数名.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 参数位置：path|query|header|cookie.
    /// </summary>
    public string In { get; set; } = "query";

    /// <summary>
    /// 是否必填.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// OpenApi 接口操作信息（含 HTTP 方法与参数），用于运行时调用.
/// </summary>
public sealed class OpenApiOperationInfo
{
    /// <summary>
    /// 操作名称（OperationId）.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// api 路径模板.
    /// </summary>
    public string Path { get; set; } = default!;

    /// <summary>
    /// HTTP 方法（大写）.
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// 参数列表.
    /// </summary>
    public IReadOnlyList<OpenApiParameterInfo> Parameters { get; set; } = [];
}
