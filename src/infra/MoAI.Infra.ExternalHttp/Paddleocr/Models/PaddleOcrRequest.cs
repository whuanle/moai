using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// OCR请求参数.
/// </summary>
public class PaddleOcrRequest
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
    /// 文本检测的图像边长限制.
    /// </summary>
    [JsonPropertyName("textDetLimitSideLen")]
    public int? TextDetLimitSideLen { get; set; }

    /// <summary>
    /// 文本检测的边长度限制类型.
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

    /// <summary>
    /// 可视化.
    /// </summary>
    [JsonPropertyName("visualize")]
    public bool? Visualize { get; set; }
}
