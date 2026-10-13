#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Metaso.Models;

/// <summary>
/// 秘塔搜索 API（/api/v1/search）请求体.
/// </summary>
public class MetasoSearchQuery
{
    /// <summary>
    /// 搜索查询关键词.
    /// </summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>
    /// 搜索范围：webpage（网页）、document（文库）、paper（论文）、image（图片）、video（视频）、podcast（播客）.
    /// </summary>
    public string Scope { get; set; } = "webpage";

    /// <summary>
    /// 是否通过网页摘要增强召回（开启后响应附带综合摘要 summary）.
    /// </summary>
    public bool IncludeSummary { get; set; }

    /// <summary>
    /// 是否抓取所有来源网页原文.
    /// </summary>
    public bool IncludeRawContent { get; set; }

    /// <summary>
    /// 返回结果数量（1-100，默认 10）.
    /// </summary>
    public int? Size { get; set; }

    /// <summary>
    /// 是否返回精简的原文匹配信息.
    /// </summary>
    public bool ConciseSnippet { get; set; }
}
