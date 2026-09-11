using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PP-OCRv5 通用文字识别请求参数.
/// </summary>
/// <remarks>
/// 参数语义与 PaddleOCR 官方 PP-OCRv5 接口一致；参数缺省时由 PaddleOCR 服务端使用默认值.
/// </remarks>
public class PaddleOcrRequest
{
    /// <summary>
    /// 图像或 PDF 文件 URL，亦可填入 Base64 编码内容.
    /// </summary>
    [Description("图像或 PDF 文件 URL，亦可直接填入 Base64 编码内容（建议在 https:// 等可被服务端访问的 URL；本地路径无法被访问）")]
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// 文件类型：0=PDF，1=图像；缺省时由 PaddleOCR 服务端根据 URL 推断.
    /// </summary>
    [Description("文件类型：0=PDF，1=图像；缺省时由服务端根据 URL 推断")]
    public int? FileType { get; set; }

    /// <summary>
    /// 是否在推理时使用文档方向分类模块.
    /// </summary>
    [Description("是否使用文档方向分类模块（默认 false）")]
    public bool? UseDocOrientationClassify { get; set; }

    /// <summary>
    /// 是否在推理时使用文本图像矫正模块.
    /// </summary>
    [Description("是否使用文本图像矫正模块（默认 false）")]
    public bool? UseDocUnwarping { get; set; }

    /// <summary>
    /// 是否在推理时使用文本行方向分类模块.
    /// </summary>
    [Description("是否使用文本行方向分类模块（默认 false）")]
    public bool? UseTextlineOrientation { get; set; }
}