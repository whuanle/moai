using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Authorization.Commands;

/// <summary>
/// 批量授权模型给某个团队.
/// </summary>
public class BatchAuthorizeModelsToTeamCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 要授权的模型ID列表.
    /// </summary>
    public IReadOnlyCollection<int> ModelIds { get; init; } = new List<int>();
}
