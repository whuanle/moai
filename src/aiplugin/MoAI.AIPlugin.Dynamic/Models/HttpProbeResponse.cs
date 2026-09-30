using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 可用性拨测插件响应。拨测失败（连不上/超时/被守卫拒绝）不是异常而是 <see cref="Ok"/>=false 加 <see cref="Error"/> 说明，
/// 便于 AI 把“不可达”当作诊断结论而不是插件故障；仅参数非法才直接报错.
/// </summary>
public class HttpProbeResponse
{
    /// <summary>
    /// 实际执行的模式.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// 归一后的拨测目标（http 为完整 URL，tcp 为 host:port，dns 为主机名）.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 是否可用（http=状态码 &lt; 400；tcp=连接成功；dns=解析出地址）.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// 不可用原因（连接拒绝/超时/解析失败/内网拒绝等，可空）.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 整次拨测耗时（毫秒）.
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// http 模式：HTTP 状态码.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// http 模式：状态短语.
    /// </summary>
    public string? ReasonPhrase { get; set; }

    /// <summary>
    /// http 模式：跟随重定向后的最终 URL.
    /// </summary>
    public string? FinalUrl { get; set; }

    /// <summary>
    /// http 模式：响应 Content-Type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// http 模式：响应 Server 头.
    /// </summary>
    public string? Server { get; set; }

    /// <summary>
    /// http 模式：响应正文预览（按 BodyPreviewChars 截断）.
    /// </summary>
    public string? BodyPreview { get; set; }

    /// <summary>
    /// tcp 模式：实际连接的远端地址.
    /// </summary>
    public string? RemoteAddress { get; set; }

    /// <summary>
    /// dns 模式：解析到的地址列表.
    /// </summary>
    public IReadOnlyList<string> Addresses { get; set; } = new List<string>();
}
