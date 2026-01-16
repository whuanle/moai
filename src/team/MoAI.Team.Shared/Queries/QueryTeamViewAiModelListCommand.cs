using MediatR;
using MoAI.AI.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 按团队角度查询可用的 AI 模型列表（包含公开模型和团队被授权的模型）.
/// </summary>
public class QueryTeamViewAiModelListCommand : IRequest<QueryTeamViewAiModelListCommandResponse>
{
    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 筛选模型类型.
    /// </summary>
    public AiModelType? AiModelType { get; set; }
}
