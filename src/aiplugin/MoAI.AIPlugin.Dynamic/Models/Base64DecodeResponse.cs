using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Base64 解码插件响应.
/// </summary>
public class Base64DecodeResponse
{
    /// <summary>
    /// 内容类型标记.
    /// </summary>
    [Description("内容类型：text=文本 image=图片 binary=其它二进制")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 检测到的 MIME 类型.
    /// </summary>
    [Description("检测到的 MIME 类型（文本为 text/plain，图片如 image/png）")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 解码后的字节数.
    /// </summary>
    [Description("解码后的字节数")]
    public int SizeBytes { get; set; }

    /// <summary>
    /// 解码文本.
    /// </summary>
    [Description("Kind=text 时的解码文本；超过配置的 TextMaxLength 时被截断")]
    public string? Text { get; set; }

    /// <summary>
    /// 文本是否被截断.
    /// </summary>
    [Description("解码文本是否因超过 TextMaxLength 被截断")]
    public bool TextTruncated { get; set; }

    /// <summary>
    /// 转存后的公开访问地址.
    /// </summary>
    [Description("Kind=image/binary 时内容转存后的公开访问地址（/static 路径，可直接访问）")]
    public string? Url { get; set; }

    /// <summary>
    /// 可直接引用的 Markdown.
    /// </summary>
    [Description("可直接放进回复的 Markdown：图片为 ![名称](地址)，其它二进制为 [名称](地址) 链接")]
    public string? Markdown { get; set; }

    /// <summary>
    /// 结果文件名.
    /// </summary>
    [Description("结果文件名（请求提供 FileName 时回显，否则按检测结果生成）")]
    public string FileName { get; set; } = string.Empty;
}
