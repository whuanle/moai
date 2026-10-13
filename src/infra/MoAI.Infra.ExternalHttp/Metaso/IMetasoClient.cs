using MoAI.Infra.Metaso.Models;
using Refit;

namespace MoAI.Infra.Metaso;

/// <summary>
/// 秘塔 AI 搜索 API 客户端（https://metaso.cn）：多范围搜索与网页阅读.
/// </summary>
public interface IMetasoClient
{
    /// <summary>
    /// 搜索：按 scope 检索网页/文库/论文/图片/视频/播客，返回命中结果列表（开启摘要召回时附带综合摘要）.
    /// </summary>
    /// <param name="authorization">Authorization 头，形如 <c>Bearer mk-xxx</c>.</param>
    /// <param name="request">请求体.</param>
    /// <returns>搜索结果.</returns>
    [Post("/api/v1/search")]
    Task<MetasoSearchPayload> SearchAsync(
        [Header("Authorization")] string authorization,
        [Body] MetasoSearchQuery request);

    /// <summary>
    /// 阅读（Markdown）：抓取目标页面并转换为 LLM 友好的 Markdown 纯文本.
    /// </summary>
    /// <param name="authorization">Authorization 头，形如 <c>Bearer mk-xxx</c>.</param>
    /// <param name="request">请求体.</param>
    /// <returns>Markdown 正文（响应体原文）.</returns>
    [Post("/api/v1/reader")]
    [Headers("Accept: text/plain")]
    Task<string> ReadMarkdownAsync(
        [Header("Authorization")] string authorization,
        [Body] MetasoReaderQuery request);

    /// <summary>
    /// 阅读（JSON）：抓取目标页面并返回带标题/作者/日期的结构化结果.
    /// </summary>
    /// <param name="authorization">Authorization 头，形如 <c>Bearer mk-xxx</c>.</param>
    /// <param name="request">请求体.</param>
    /// <returns>结构化阅读结果.</returns>
    [Post("/api/v1/reader")]
    [Headers("Accept: application/json")]
    Task<MetasoReaderResult> ReadJsonAsync(
        [Header("Authorization")] string authorization,
        [Body] MetasoReaderQuery request);
}
