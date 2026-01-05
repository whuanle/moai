using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Authorization.Commands;

/// <summary>
/// 修改某个插件的授权团队列表.
/// </summary>
public class UpdatePluginAuthorizationsCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 插件ID.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 授权的团队ID列表（覆盖式更新）.
    /// </summary>
    public IReadOnlyCollection<int> TeamIds { get; init; } = new List<int>();
}
