using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;


/// <summary>
/// 表示解析结果中的单个页面
/// </summary>
public class Doc2xPage
{
    /// <summary>
    /// 当前页面的图片URL
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }

    /// <summary>
    /// 当前页面的索引，从0开始
    /// </summary>
    [JsonPropertyName("page_idx")]
    public int PageIdx { get; set; }

    /// <summary>
    /// 当前页面的宽度（单位：像素）
    /// </summary>
    [JsonPropertyName("page_width")]
    public int PageWidth { get; set; }

    /// <summary>
    /// 当前页面的高度（单位：像素）
    /// </summary>
    [JsonPropertyName("page_height")]
    public int PageHeight { get; set; }

    /// <summary>
    /// 当前页面的Markdown格式内容
    /// </summary>
    [JsonPropertyName("md")]
    public string Md { get; set; }
}