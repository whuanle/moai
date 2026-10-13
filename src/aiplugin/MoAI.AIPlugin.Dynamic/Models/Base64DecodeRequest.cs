using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Base64 解码插件请求.
/// </summary>
public class Base64DecodeRequest
{
    /// <summary>
    /// 待解码的 Base64 字符串.
    /// </summary>
    [Description("待解码的 Base64 字符串，允许含空白与换行；也支持 data:image/png;base64,xxxx 形式（自动剥离前缀识别类型）")]
    public string Base64 { get; set; } = string.Empty;

    /// <summary>
    /// 可选文件名.
    /// </summary>
    [Description("可选文件名（如 photo.png），仅用于推断转存文件的扩展名与结果回显；未提供时按内容检测结果命名")]
    public string FileName { get; set; } = string.Empty;
}
