using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 表示解析结果的类
/// </summary>
public class Doc2xParseResult
{
    /// <summary>
    /// 解析结果的版本号
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; }

    /// <summary>
    /// 解析结果的页面列表
    /// </summary>
    [JsonPropertyName("pages")]
    public List<Doc2xPage> Pages { get; set; }
}
