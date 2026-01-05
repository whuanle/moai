using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Authorization.Commands;

/// <summary>
/// 批量撤销某个团队的模型授权.
/// </summary>
public class BatchRevokeModelsFromTeamCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 要撤销的模型ID列表.
    /// </summary>
    public IReadOnlyCollection<int> ModelIds { get; init; } = new List<int>();
}
