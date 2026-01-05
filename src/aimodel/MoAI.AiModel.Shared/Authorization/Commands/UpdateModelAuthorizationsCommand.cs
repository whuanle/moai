using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Authorization.Commands;

/// <summary>
/// 修改某个模型的授权团队列表.
/// </summary>
public class UpdateModelAuthorizationsCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 模型ID.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 授权的团队ID列表（覆盖式更新）.
    /// </summary>
    public IReadOnlyCollection<int> TeamIds { get; init; } = new List<int>();
}
