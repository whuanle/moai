using System.Text.Json.Serialization;

namespace MoAI.Wiki.Models;

public enum WebDocumentCrawlPathRule
{
    /// <summary>
    /// 忽略.
    /// </summary>
    [JsonPropertyName("ignore")]
    Ignore = 0,

    /// <summary>
    /// 覆盖.
    /// </summary>
    [JsonPropertyName("rewriter")]
    Rewrite = 0,
}
