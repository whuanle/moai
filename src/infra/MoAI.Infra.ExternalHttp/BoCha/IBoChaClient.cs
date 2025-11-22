using MoAI.Infra.BoCha.Models;
using MoAI.Infra.Exceptions;
using Refit;

namespace MoAI.Infra.BoCha;

/// <summary>
/// BoCha API 客户端接口
/// </summary>
public interface IBoChaClient
{
    /// <summary>
    /// Web Search API - 全网搜索
    /// </summary>
    /// <param name="key">API Key</param>
    /// <param name="request">搜索请求参数</param>
    /// <returns>搜索结果</returns>
    [Post("/v1/web-search")]
    Task<WebSearchResponse> WebSearchAsync([Header("Authorization")] string key, [Body] WebSearchRequest request);

    /// <summary>
    /// AI Search API - 混合搜索
    /// </summary>
    /// <param name="key">API Key</param>
    /// <param name="request">搜索请求参数</param>
    /// <returns>搜索结果</returns>
    [Post("/v1/ai-search")]
    Task<AiSearchResponse> AiSearchAsync([Header("Authorization")] string key, [Body] AiSearchRequest request);

    /// <summary>
    /// Semantic Rerank API - 语义排序
    /// </summary>
    /// <param name="key">API Key</param>
    /// <param name="request">排序请求参数</param>
    /// <returns>排序结果</returns>
    [Post("/v1/rerank")]
    Task<SemanticRerankResponse> SemanticRerankAsync([Header("Authorization")] string key, [Body] SemanticRerankRequest request);

    /// <summary>
    /// 处理 API 响应错误
    /// </summary>
    /// <param name="response">响应对象</param>
    /// <typeparam name="T">T.</typeparam>
    public void HandleApiError<T>(T response)
        where T : BoChaCode
    {
        if (response == null)
        {
            throw new BusinessException("API 响应为空");
        }

        if (response.Code == 200)
        {
            return;
        }

        switch (response.Code)
        {
            case 400:
                throw new BusinessException("请求参数错误: {0}", response.Msg);
            case 401:
                throw new BusinessException("未授权: {0}", response.Msg);
            case 403:
                throw new BusinessException("余额不足: {0}", response.Msg);
            case 429:
                throw new BusinessException("请求频率限制: {0}", response.Msg);
            case 500:
                throw new BusinessException("服务器内部错误: {0}", response.Msg);
            default:
                throw new BusinessException("未知错误: {0}", response.Msg);
        }
    }
}
