using System;
using System.Collections.Generic;
using MoAI.AIChannel.Queries.Responses;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// 渠道级授权查询响应.
/// </summary>
public class QueryAIChannelAuthorizationCommandResponse
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public Guid ChannelId { get; set; }

    /// <summary>
    /// 渠道下模型总数.
    /// </summary>
    public int ModelCount { get; set; }

    /// <summary>
    /// 私有模型数（渠道级授权的作用范围）.
    /// </summary>
    public int PrivateModelCount { get; set; }

    /// <summary>
    /// 公开模型数（不受渠道级授权影响）.
    /// </summary>
    public int PublicModelCount { get; set; }

    /// <summary>
    /// 各私有模型授权团队的并集（按团队 id 升序去重）.
    /// </summary>
    public IReadOnlyList<QueryAIModelAuthorizationCommandResponseItem> Items { get; set; } = new List<QueryAIModelAuthorizationCommandResponseItem>();
}
