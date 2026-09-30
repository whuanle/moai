using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// ClickStack 查询插件配置（每个实例独立保存）.
/// </summary>
public class ClickStackQueryConfig
{
    /// <summary>
    /// ClickStack 对外 API 基址.
    /// </summary>
    [Description("ClickStack/HyperDX 对外 API 基址（API server，镜像默认 8000 端口，与 UI 的 8080 分离），例如 http://192.168.50.199:28000")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Personal API Access Key.
    /// </summary>
    [Description("Personal API Access Key：HyperDX UI → Team Settings → API Keys 创建；注意不是 OTLP 摄入用的 Ingestion Key")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// search 模式默认行数上限（1-500）.
    /// </summary>
    [Description("search 模式单次返回行数上限，取值 1-500（默认 100）；单次请求可用 MaxResults 覆盖，服务端硬上限 2000")]
    public int MaxRows { get; set; } = 100;
}
