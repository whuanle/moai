using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 删除系统应用接入（root，软删除）：删除后 key 立即失效.
/// </summary>
public class DeleteSystemAccessAppCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 接入 id（由路由填充）.
    /// </summary>
    public Guid Id { get; set; }
}
