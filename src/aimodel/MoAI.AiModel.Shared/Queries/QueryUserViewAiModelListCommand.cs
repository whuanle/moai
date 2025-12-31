using MediatR;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询用户可见的 AI 模型列表.
/// </summary>
public class QueryUserViewAiModelListCommand : IRequest<QueryUserViewAiModelListCommandResponse>
{
    /// <summary>
    /// 筛选模型类型.
    /// </summary>
    public AiModelType? AiModelType { get; set; }
}