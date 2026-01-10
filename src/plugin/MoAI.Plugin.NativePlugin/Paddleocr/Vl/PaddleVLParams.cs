using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Paddleocr.Vl;

/// <summary>
/// PaddleOCR-VL 插件参数.
/// </summary>
public class PaddleVLParams
{
    /// <summary>
    /// /// 文档文件URL或Base64编码内容.
    /// </summary>
    [JsonPropertyName("file")]
    [Description("文档文件URL或Base64编码内容")]
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// 文件类型 (0=PDF, 1=图像).
    /// </summary>
    [JsonPropertyName("fileType")]
    [Description("文件类型 (0=PDF, 1=图像)")]
    public int? FileType { get; set; }

    /// <summary>
    /// 是否使用文档方向分类.
    /// </summary>
    [JsonPropertyName("useDocOrientationClassify")]
    [Description("是否使用文档方向分类")]
    public bool? UseDocOrientationClassify { get; set; }

    /// <summary>
    /// 是否使用文本图像矫正.
    /// </summary>
    [JsonPropertyName("useDocUnwarping")]
    [Description("是否使用文本图像矫正")]
    public bool? UseDocUnwarping { get; set; }

    /// <summary>
    /// 是否使用版面区域检测排序.
    /// </summary>
    [JsonPropertyName("useLayoutDetection")]
    [Description("是否使用版面区域检测排序")]
    public bool? UseLayoutDetection { get; set; }

    /// <summary>
    /// 是否使用图表解析.
    /// </summary>
    [JsonPropertyName("useChartRecognition")]
    [Description("是否使用图表解析")]
    public bool? UseChartRecognition { get; set; }

    /// <summary>
    /// 是否输出美化后的 Markdown.
    /// </summary>
    [JsonPropertyName("prettifyMarkdown")]
    [Description("是否输出美化后的 Markdown")]
    public bool? PrettifyMarkdown { get; set; }

    /// <summary>
    /// Markdown 中是否包含公式编号.
    /// </summary>
    [JsonPropertyName("showFormulaNumber")]
    [Description("Markdown 中是否包含公式编号")]
    public bool? ShowFormulaNumber { get; set; }
}
