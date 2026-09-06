using System;
using System.Collections.Generic;
using MoAI.AIChannel.Models;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// 模型授权与额度查询响应.
/// </summary>
public class QueryAIModelAuthorizationCommandResponse
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 是否公开：true=所有团队可用，可设全局额度；false=私有，仅授权团队可用.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 全局额度（公开模型，TeamId=0 的规则），未设置时为 null.
    /// </summary>
    public AIModelQuotaInfo? GlobalQuota { get; set; }

    /// <summary>
    /// 授权团队集合（私有模型），每项携带该团队当前额度.
    /// </summary>
    public IReadOnlyList<QueryAIModelAuthorizationCommandResponseItem> Items { get; set; } = new List<QueryAIModelAuthorizationCommandResponseItem>();
}
