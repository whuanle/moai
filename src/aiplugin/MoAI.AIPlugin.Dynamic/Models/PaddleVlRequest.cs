using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PaddleOCR-VL 视觉语言模型文档解析请求参数.
/// </summary>
/// <remarks>
/// 视觉语言模型同时识别文字、版面、图表、公式并直接产出 Markdown；适合复杂文档.
/// </remarks>
public class PaddleVlRequest
{
    /// <summary>
    /// 文档文件 URL，亦可填入 Base64 编码内容.
    /// </summary>
    [Description("文档文件 URL，亦可直接填入 Base64 编码内容（建议在 https:// 等可被服务端访问的 URL；本地路径无法被访问）")]
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
    /// 是否在推理时使用版面区域检测排序模块.
    /// </summary>
    [Description("是否使用版面区域检测排序模块（默认 false）")]
    public bool? UseLayoutDetection { get; set; }

    /// <summary>
    /// 是否在推理时使用图表解析模块.
    /// </summary>
    [Description("是否使用图表解析模块（默认 false）")]
    public bool? UseChartRecognition { get; set; }

    /// <summary>
    /// 是否输出美化后的 Markdown（更强的格式整理，耗时略增）.
    /// </summary>
    [Description("是否输出美化后的 Markdown（默认 false）；启用后会做更精细的格式整理")]
    public bool? PrettifyMarkdown { get; set; }

    /// <summary>
    /// Markdown 中是否包含公式编号.
    /// </summary>
    [Description("Markdown 中是否包含公式编号（默认 false）")]
    public bool? ShowFormulaNumber { get; set; }
}