using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PP-StructureV3 文档解析请求参数.
/// </summary>
/// <remarks>
/// 参数语义与 PaddleOCR 官方 PP-StructureV3 接口一致；适合处理带表格、公式、印章、图表的复杂文档.
/// </remarks>
public class PaddleStructureV3Request
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
    /// 是否启用表格识别子产线.
    /// </summary>
    [Description("是否启用表格识别子产线（默认 false）")]
    public bool? UseTableRecognition { get; set; }

    /// <summary>
    /// 是否启用公式识别子产线.
    /// </summary>
    [Description("是否启用公式识别子产线（默认 false）")]
    public bool? UseFormulaRecognition { get; set; }

    /// <summary>
    /// 是否启用印章识别子产线.
    /// </summary>
    [Description("是否启用印章识别子产线（默认 false）；启用时响应 SealTexts 会填充识别结果")]
    public bool? UseSealRecognition { get; set; }

    /// <summary>
    /// 是否启用图表解析模块.
    /// </summary>
    [Description("是否启用图表解析模块（默认 false）")]
    public bool? UseChartRecognition { get; set; }

    /// <summary>
    /// 是否启用文档区域检测模块.
    /// </summary>
    [Description("是否启用文档区域检测模块（默认 false）")]
    public bool? UseRegionDetection { get; set; }
}