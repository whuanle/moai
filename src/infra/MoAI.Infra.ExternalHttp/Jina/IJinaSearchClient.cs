using System.Collections.Generic;
using MoAI.Infra.Jina.Models;
using Refit;

namespace MoAI.Infra.Jina;

/// <summary>
/// Jina AI Search API 客户端（https://s.jina.ai）.
/// </summary>
public interface IJinaSearchClient
{
    /// <summary>
    /// 搜索：检索全网并对命中页面做正文提取，一次返回标题、摘要与 LLM 友好正文.
    /// </summary>
    /// <param name="authorization">Authorization 头，形如 <c>Bearer jina_xxx</c>.</param>
    /// <param name="returnFormat">X-Return-Format 头：返回内容格式，null 表示使用服务端默认（Markdown）.</param>
    /// <param name="noCache">X-No-Cache 头：是否绕过缓存，null 表示不发送.</param>
    /// <param name="request">请求体.</param>
    /// <returns>搜索结果数组.</returns>
    [Post("/")]
    Task<JinaEnvelope<List<JinaSearchResultItem>>> SearchAsync(
        [Header("Authorization")] string authorization,
        [Header("X-Return-Format")] string? returnFormat,
        [Header("X-No-Cache")] string? noCache,
        [Body] JinaSearchQuery request);
}
