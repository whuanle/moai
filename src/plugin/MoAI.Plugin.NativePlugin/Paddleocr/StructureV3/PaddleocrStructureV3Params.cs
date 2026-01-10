using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.Paddleocr.StructureV3;

/// <summary>
/// Paddleocr StructureV3 插件参数.
/// </summary>
public class PaddleocrStructureV3Params
{
    /// <summary>
    /// 文档文件URL或Base64编码内容.
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
    /// 是否使用表格识别.
    /// </summary>
    [JsonPropertyName("useTableRecognition")]
    [Description("是否使用表格识别")]
    public bool? UseTableRecognition { get; set; }

    /// <summary>
    /// 是否使用公式识别.
    /// </summary>
    [JsonPropertyName("useFormulaRecognition")]
    [Description("是否使用公式识别")]
    public bool? UseFormulaRecognition { get; set; }

    /// <summary>
    /// 是否使用印章识别.
    /// </summary>
    [JsonPropertyName("useSealRecognition")]
    [Description("是否使用印章识别")]
    public bool? UseSealRecognition { get; set; }

    /// <summary>
    /// 是否使用图表解析.
    /// </summary>
    [JsonPropertyName("useChartRecognition")]
    [Description("是否使用图表解析")]
    public bool? UseChartRecognition { get; set; }

    /// <summary>
    /// 是否使用文档区域检测.
    /// </summary>
    [JsonPropertyName("useRegionDetection")]
    [Description("是否使用文档区域检测")]
    public bool? UseRegionDetection { get; set; }
}
