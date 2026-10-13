using MoAI.Infra.Jina.Models;
using Refit;

namespace MoAI.Infra.Jina;

/// <summary>
/// Jina AI Reader API 客户端（https://r.jina.ai）：把网页（含需要 JS 渲染的页面）转换为 LLM 友好内容.
/// </summary>
public interface IJinaReaderClient
{
    /// <summary>
    /// 阅读：抓取目标页面并转换为 Markdown 等格式，可附带链接/图片摘要.
    /// </summary>
    /// <param name="authorization">Authorization 头，形如 <c>Bearer jina_xxx</c>.</param>
    /// <param name="accept">Accept 头，传 application/json 获取结构化响应.</param>
    /// <param name="returnFormat">X-Return-Format 头：返回内容格式，null 表示使用服务端默认（Markdown）.</param>
    /// <param name="withLinksSummary">X-With-Links-Summary 头：是否附带链接摘要，null 表示不发送.</param>
    /// <param name="withImagesSummary">X-With-Images-Summary 头：是否附带图片摘要，null 表示不发送.</param>
    /// <param name="noCache">X-No-Cache 头：是否绕过缓存，null 表示不发送.</param>
    /// <param name="timeout">X-Timeout 头：抓取超时秒数，null 表示不发送.</param>
    /// <param name="request">请求体.</param>
    /// <returns>单条阅读结果.</returns>
    [Post("/")]
    Task<JinaEnvelope<JinaReaderResult>> ReadAsync(
        [Header("Authorization")] string authorization,
        [Header("Accept")] string accept,
        [Header("X-Return-Format")] string? returnFormat,
        [Header("X-With-Links-Summary")] string? withLinksSummary,
        [Header("X-With-Images-Summary")] string? withImagesSummary,
        [Header("X-No-Cache")] string? noCache,
        [Header("X-Timeout")] string? timeout,
        [Body] JinaReaderQuery request);
}
