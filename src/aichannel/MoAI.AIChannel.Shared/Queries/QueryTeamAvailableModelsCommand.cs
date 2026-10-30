using System;
using MediatR;
using MoAI.AIChannel.Queries.Responses;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// 查询团队可用模型列表（团队自有渠道 ∪ 公开模型 ∪ 已授权模型，模型与渠道均启用），团队成员可访问；
/// 供应用配置、流程节点等模型选择器取值.
/// </summary>
public class QueryTeamAvailableModelsCommand : IRequest<QueryTeamAvailableModelsCommandResponse>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }
}
