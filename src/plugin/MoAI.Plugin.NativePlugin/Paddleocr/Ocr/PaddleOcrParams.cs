using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Paddleocr.Ocr;

/// <summary>
/// Paddleocr 插件通用参数.
/// </summary>
public class PaddleOcrParams
{
    /// <summary>
    /// 图像文件URL或Base64编码内容.
    /// </summary>
    [JsonPropertyName("file")]
    [Description("图像文件URL或Base64编码内容")]
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
    /// 是否使用文本行方向分类.
    /// </summary>
    [JsonPropertyName("useTextlineOrientation")]
    [Description("是否使用文本行方向分类")]
    public bool? UseTextlineOrientation { get; set; }
}
