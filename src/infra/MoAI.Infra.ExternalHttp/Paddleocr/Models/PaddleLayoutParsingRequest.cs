using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// 文档解析请求参数 (PP-StructureV3).
/// </summary>
public class PaddleLayoutParsingRequest
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
    /// 是否在推理时使用文档方向分类模块.
    /// </summary>
    [JsonPropertyName("useDocOrientationClassify")]
    public bool? UseDocOrientationClassify { get; set; }

    /// <summary>
    /// 是否在推理时使用文本图像矫正模块.
    /// </summary>
    [JsonPropertyName("useDocUnwarping")]
    public bool? UseDocUnwarping { get; set; }

    /// <summary>
    /// 是否在推理时使用文本行方向分类模块.
    /// </summary>
    [JsonPropertyName("useTextlineOrientation")]
    public bool? UseTextlineOrientation { get; set; }

    /// <summary>
    /// 是否在推理时使用印章文本识别子产线.
    /// </summary>
    [JsonPropertyName("useSealRecognition")]
    public bool? UseSealRecognition { get; set; }

    /// <summary>
    /// 是否在推理时使用表格识别子产线.
    /// </summary>
    [JsonPropertyName("useTableRecognition")]
    public bool? UseTableRecognition { get; set; }

    /// <summary>
    /// 是否在推理时使用公式识别子产线.
    /// </summary>
    [JsonPropertyName("useFormulaRecognition")]
    public bool? UseFormulaRecognition { get; set; }

    /// <summary>
    /// 是否在推理时使用图表解析模块.
    /// </summary>
    [JsonPropertyName("useChartRecognition")]
    public bool? UseChartRecognition { get; set; }

    /// <summary>
    /// 是否在推理时使用文档区域检测模块.
    /// </summary>
    [JsonPropertyName("useRegionDetection")]
    public bool? UseRegionDetection { get; set; }

    /// <summary>
    /// 版面模型得分阈值.
    /// </summary>
    [JsonPropertyName("layoutThreshold")]
    public double? LayoutThreshold { get; set; }

    /// <summary>
    /// 开启后，会自动移除重复或高度重叠的区域框.
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

    // --- 文本检测相关 ---

    /// <summary>
    /// 文本检测的图像边长限制.
    /// </summary>
    [JsonPropertyName("textDetLimitSideLen")]
    public int? TextDetLimitSideLen { get; set; }

    /// <summary>
    /// 文本检测的边长度限制类型 (min, max).
    /// </summary>
    [JsonPropertyName("textDetLimitType")]
    public string? TextDetLimitType { get; set; }

    /// <summary>
    /// 文本检测像素阈值.
    /// </summary>
    [JsonPropertyName("textDetThresh")]
    public double? TextDetThresh { get; set; }

    /// <summary>
    /// 文本检测框阈值.
    /// </summary>
    [JsonPropertyName("textDetBoxThresh")]
    public double? TextDetBoxThresh { get; set; }

    /// <summary>
    /// 文本检测扩张系数.
    /// </summary>
    [JsonPropertyName("textDetUnclipRatio")]
    public double? TextDetUnclipRatio { get; set; }

    /// <summary>
    /// 文本识别阈值.
    /// </summary>
    [JsonPropertyName("textRecScoreThresh")]
    public double? TextRecScoreThresh { get; set; }

    // --- 印章检测相关 ---

    /// <summary>
    /// 印章文本检测的图像边长限制.
    /// </summary>
    [JsonPropertyName("sealDetLimitSideLen")]
    public int? SealDetLimitSideLen { get; set; }

    /// <summary>
    /// 印章文本检测的图像边长限制类型.
    /// </summary>
    [JsonPropertyName("sealDetLimitType")]
    public string? SealDetLimitType { get; set; }

    /// <summary>
    /// 印章检测像素阈值.
    /// </summary>
    [JsonPropertyName("sealDetThresh")]
    public double? SealDetThresh { get; set; }

    /// <summary>
    /// 印章文本检测框阈值.
    /// </summary>
    [JsonPropertyName("sealDetBoxThresh")]
    public double? SealDetBoxThresh { get; set; }

    /// <summary>
    /// 印章检测扩张系数.
    /// </summary>
    [JsonPropertyName("sealDetUnclipRatio")]
    public double? SealDetUnclipRatio { get; set; }

    /// <summary>
    /// 印章文本识别阈值.
    /// </summary>
    [JsonPropertyName("sealRecScoreThresh")]
    public double? SealRecScoreThresh { get; set; }

    // --- 表格相关 ---

    /// <summary>
    /// 是否启用有线表单元格检测结果直转HTML.
    /// </summary>
    [JsonPropertyName("useWiredTableCellsTransToHtml")]
    public bool? UseWiredTableCellsTransToHtml { get; set; }

    /// <summary>
    /// 是否启用无线表单元格检测结果直转HTML.
    /// </summary>
    [JsonPropertyName("useWirelessTableCellsTransToHtml")]
    public bool? UseWirelessTableCellsTransToHtml { get; set; }

    /// <summary>
    /// 是否启用表格使用表格方向分类.
    /// </summary>
    [JsonPropertyName("useTableOrientationClassify")]
    public bool? UseTableOrientationClassify { get; set; }

    /// <summary>
    /// 是否启用单元格切分OCR.
    /// </summary>
    [JsonPropertyName("useOcrResultsWithTableCells")]
    public bool? UseOcrResultsWithTableCells { get; set; }

    /// <summary>
    /// 是否启用有线表端到端表格识别模式.
    /// </summary>
    [JsonPropertyName("useE2eWiredTableRecModel")]
    public bool? UseE2eWiredTableRecModel { get; set; }

    /// <summary>
    /// 是否启用无线表端到端表格识别模式.
    /// </summary>
    [JsonPropertyName("useE2eWirelessTableRecModel")]
    public bool? UseE2eWirelessTableRecModel { get; set; }

    /// <summary>
    /// 可视化.
    /// </summary>
    [JsonPropertyName("visualize")]
    public bool? Visualize { get; set; }
}
