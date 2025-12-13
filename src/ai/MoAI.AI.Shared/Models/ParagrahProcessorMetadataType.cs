using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MoAI.AI.Models;

/// <summary>
/// 元数据类型.
/// </summary>
public enum ParagrahProcessorMetadataType
{
    /// <summary>
    /// 生成的大纲.
    /// </summary>
    [JsonPropertyName("outline")]
    Outline,

    /// <summary>
    /// 提问.
    /// </summary>
    [JsonPropertyName("question")]
    Question,

    /// <summary>
    /// 关键词.
    /// </summary>
    [JsonPropertyName("keyword")]
    Keyword,

    /// <summary>
    /// 摘要.
    /// </summary>
    [JsonPropertyName("summary")]
    Summary,

    /// <summary>
    /// 按语义重新聚合的段.
    /// </summary>
    [JsonPropertyName("aggregatedSubParagraph")]
    AggregatedSubParagraph
}
