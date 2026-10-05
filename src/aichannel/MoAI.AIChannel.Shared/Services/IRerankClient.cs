using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 重排序客户端：调用渠道的重排序模型，对文档集合按与查询的相关性重新打分.
/// </summary>
public interface IRerankClient
{
    /// <summary>
    /// 调用重排序模型，返回各文档与查询的相关性得分（顺序不保证，按 <see cref="RerankResultItem.Index"/> 对应输入）.
    /// </summary>
    /// <param name="model">重排序模型.</param>
    /// <param name="channel">模型所在渠道.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="documents">待排序文档内容集合.</param>
    /// <param name="topN">返回前 N 条结果；null 表示全部返回.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回重排序结果集合.</returns>
    Task<IReadOnlyList<RerankResultItem>> RerankAsync(AiModelEntity model, AiChannelEntity channel, string query, IReadOnlyList<string> documents, int? topN = null, CancellationToken cancellationToken = default);
}
