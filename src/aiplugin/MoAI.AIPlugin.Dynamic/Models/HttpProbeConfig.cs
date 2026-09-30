using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 可用性拨测插件配置（每个实例独立保存）.
/// </summary>
public class HttpProbeConfig
{
    /// <summary>
    /// 单次拨测超时秒数（1-120）.
    /// </summary>
    [Description("单次拨测超时秒数，取值 1-120（默认 10）")]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// http 模式响应正文预览最大字符数（0-4096）.
    /// </summary>
    [Description("http 模式响应正文预览最大字符数，取值 0-4096（默认 512）；0 表示不返回正文预览")]
    public int BodyPreviewChars { get; set; } = 512;

    /// <summary>
    /// 是否允许拨测内网地址.
    /// </summary>
    [Description("是否允许拨测内网地址（回环/RFC1918 私有网段/链路本地/IPv6 ULA）。默认 false：目标解析到内网 IP 时直接返回拒绝，防止插件被用来探测内网")]
    public bool AllowPrivateNetwork { get; set; }
}
