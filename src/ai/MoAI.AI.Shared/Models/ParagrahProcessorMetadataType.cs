using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    Outline,

    /// <summary>
    /// 提问.
    /// </summary>
    Question,

    /// <summary>
    /// 关键词.
    /// </summary>
    Keyword,

    /// <summary>
    /// 摘要.
    /// </summary>
    Summary,

    /// <summary>
    /// 按语义重新聚合的段.
    /// </summary>
    AggregatedSubParagraph
}
