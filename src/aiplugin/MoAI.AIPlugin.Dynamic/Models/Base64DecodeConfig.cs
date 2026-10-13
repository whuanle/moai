using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Base64 解码插件配置（每个实例独立保存）.
/// </summary>
public class Base64DecodeConfig
{
    /// <summary>
    /// 单次解码结果的最大字节数.
    /// </summary>
    [Description("单次解码结果的最大字节数，取值 1024-33554432（默认 2097152 即 2MB）；超出直接拒绝并提示调大配置")]
    public int MaxDecodeBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// 文本输出的最大字符数.
    /// </summary>
    [Description("文本输出的最大字符数，取值 100-200000（默认 20000）；超出部分被截断并把 TextTruncated 置为 true")]
    public int TextMaxLength { get; set; } = 20000;
}
