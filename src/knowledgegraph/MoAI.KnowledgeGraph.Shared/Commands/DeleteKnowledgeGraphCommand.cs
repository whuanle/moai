using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除知识图谱，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteKnowledgeGraphCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }
}
