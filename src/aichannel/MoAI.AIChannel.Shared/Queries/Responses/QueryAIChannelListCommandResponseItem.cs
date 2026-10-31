using System;
using MoAI.AIChannel.Models;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// QueryAIChannelListCommandResponseItem.
/// </summary>
public class QueryAIChannelListCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 渠道标识.
    /// </summary>
    public string ProviderKey { get; set; } = default!;

    /// <summary>
    /// 渠道名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 协议（对应 AIProtocolFamily 枚举）.
    /// </summary>
    public AIProtocolFamily ProtocolFamily { get; set; }

    /// <summary>
    /// 接入端点，可能包含密钥脱敏.
    /// </summary>
    public string BaseUrl { get; set; } = default!;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 模型数量.
    /// </summary>
    public int ModelCount { get; set; }

    /// <summary>
    /// 渠道下模型累计消耗 tokens 总量（含输入与输出，取自 ai_model_token_audit 聚合，非实时）.
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 渠道下模型累计输入（提示）tokens.
    /// </summary>
    public long PromptTokens { get; set; }

    /// <summary>
    /// 渠道下模型累计输出（补全）tokens.
    /// </summary>
    public long CompletionTokens { get; set; }
}
