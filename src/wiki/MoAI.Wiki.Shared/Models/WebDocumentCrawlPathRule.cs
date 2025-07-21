using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
    Rewriter = 0,
}
