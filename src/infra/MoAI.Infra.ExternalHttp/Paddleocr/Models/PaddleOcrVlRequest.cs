using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// 文档解析请求参数 (PaddleOCR-VL).
/// </summary>
public class PaddleOcrVlRequest
{
    /// <summary>
    /// 服务器可访问的图像文件或PDF文件的URL，或上述类型文件内容的Base64编码结果.
    /// </summary>
    [JsonPropertyName("file")]
    public required string File { get; set; }

    /// <summary>
    /// 文件类型。0表示PDF文件，1表示图像文件。若请求体无此属性，则将根据URL推断文件类型.
    /// </summary>
    [JsonPropertyName("fileType")]
    public int? FileType { get; set; }

    /// <summary>
    /// 是否在推理时使用文本图像方向矫正模块.
    /// </summary>
    [JsonPropertyName("useDocOrientationClassify")]
    public bool? UseDocOrientationClassify { get; set; }

    /// <summary>
    /// 是否在推理时使用文本图像矫正模块.
    /// </summary>
    [JsonPropertyName("useDocUnwarping")]
    public bool? UseDocUnwarping { get; set; }

    /// <summary>
    /// 是否在推理时使用版面区域检测排序模块.
    /// </summary>
    [JsonPropertyName("useLayoutDetection")]
    public bool? UseLayoutDetection { get; set; }

    /// <summary>
    /// 是否在推理时使用图表解析模块.
    /// </summary>
    [JsonPropertyName("useChartRecognition")]
    public bool? UseChartRecognition { get; set; }

    /// <summary>
    /// 版面模型得分阈值.
    /// </summary>
    [JsonPropertyName("layoutThreshold")]
    public double? LayoutThreshold { get; set; }

    /// <summary>
    /// 版面检测是否使用后处理NMS.
    /// </summary>
    [JsonPropertyName("layoutNms")]
    public bool? LayoutNms { get; set; }

    /// <summary>
    /// 版面区域检测模型检测框的扩张系数.
    /// </summary>
    [JsonPropertyName("layoutUnclipRatio")]
    public double? LayoutUnclipRatio { get; set; }

    /// <summary>
    /// 版面区域检测的重叠框过滤方式 (large, small, union).
    /// </summary>
    [JsonPropertyName("layoutMergeBboxesMode")]
    public string? LayoutMergeBboxesMode { get; set; }

    /// <summary>
    /// VL模型的 prompt 类型设置，当且仅当 useLayoutDetection=False 时生效.
    /// </summary>
    [JsonPropertyName("promptLabel")]
    public string? PromptLabel { get; set; }

    /// <summary>
    /// 重复抑制强度.
    /// </summary>
    [JsonPropertyName("repetitionPenalty")]
    public double? RepetitionPenalty { get; set; }

    /// <summary>
    /// 识别稳定性.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    /// 结果可信范围.
    /// </summary>
    [JsonPropertyName("topP")]
    public double? TopP { get; set; }

    /// <summary>
    /// 最小图像尺寸.
    /// </summary>
    [JsonPropertyName("minPixels")]
    public int? MinPixels { get; set; }

    /// <summary>
    /// 最大图像尺寸.
    /// </summary>
    [JsonPropertyName("maxPixels")]
    public int? MaxPixels { get; set; }

    /// <summary>
    /// 输出的 Markdown 文本中是否包含公式编号.
    /// </summary>
    [JsonPropertyName("showFormulaNumber")]
    public bool? ShowFormulaNumber { get; set; }

    /// <summary>
    /// 是否输出美化后的 Markdown 文本.
    /// </summary>
    [JsonPropertyName("prettifyMarkdown")]
    public bool? PrettifyMarkdown { get; set; }

    /// <summary>
    /// 可视化.
    /// </summary>
    [JsonPropertyName("visualize")]
    public bool? Visualize { get; set; }
}
