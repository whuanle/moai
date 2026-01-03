using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// 文档解析Markdown结果.
/// </summary>
public class PaddleLayoutParsingMarkdown
{
    /// <summary>
    /// Markdown文本.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Markdown图片相对路径和Base64编码图像的键值对.
    /// </summary>
    [JsonPropertyName("images")]
    public Dictionary<string, string>? Images { get; set; }

    /// <summary>
    /// 当前页面第一个元素是否为段开始.
    /// </summary>
    [JsonPropertyName("isStart")]
    public bool IsStart { get; set; }

    /// <summary>
    /// 当前页面最后一个元素是否为段结束.
    /// </summary>
    [JsonPropertyName("isEnd")]
    public bool IsEnd { get; set; }
}