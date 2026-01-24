using MoAI.Plugin.Attributes;
using MoAI.Plugin.Plugins;
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
    [NativePluginField(
        Key = nameof(File),
        Description = "图像文件URL或Base64编码内容",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "https://example.com/document.pdf")]
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// 文件类型 (0=PDF, 1=图像).
    /// </summary>
    [JsonPropertyName("fileType")]
    [Description("文件类型 (0=PDF, 1=图像)")]
    [NativePluginField(
        Key = nameof(FileType),
        Description = "文件类型 (0=PDF, 1=图像)",
        FieldType = PluginConfigFieldType.Number,
        IsRequired = false,
        ExampleValue = "1")]
    public int? FileType { get; set; }

    /// <summary>
    /// 是否使用文档方向分类.
    /// </summary>
    [JsonPropertyName("useDocOrientationClassify")]
    [Description("是否使用文档方向分类")]
    [NativePluginField(
        Key = nameof(UseDocOrientationClassify),
        Description = "是否使用文档方向分类",
        FieldType = PluginConfigFieldType.Boolean,
        IsRequired = false,
        ExampleValue = "true")]
    public bool? UseDocOrientationClassify { get; set; }

    /// <summary>
    /// 是否使用文本图像矫正.
    /// </summary>
    [JsonPropertyName("useDocUnwarping")]
    [Description("是否使用文本图像矫正")]
    [NativePluginField(
        Key = nameof(UseDocUnwarping),
        Description = "是否使用文本图像矫正",
        FieldType = PluginConfigFieldType.Boolean,
        IsRequired = false,
        ExampleValue = "false")]
    public bool? UseDocUnwarping { get; set; }

    /// <summary>
    /// 是否使用文本行方向分类.
    /// </summary>
    [JsonPropertyName("useTextlineOrientation")]
    [Description("是否使用文本行方向分类")]
    [NativePluginField(
        Key = nameof(UseTextlineOrientation),
        Description = "是否使用文本行方向分类",
        FieldType = PluginConfigFieldType.Boolean,
        IsRequired = false,
        ExampleValue = "false")]
    public bool? UseTextlineOrientation { get; set; }
}
