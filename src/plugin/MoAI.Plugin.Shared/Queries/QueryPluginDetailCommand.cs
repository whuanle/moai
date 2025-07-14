using MediatR;

namespace MoAI.Plugin.Queries;

public class QueryPluginDetailCommand : IRequest<QueryPluginDetailCommandResponse>
{
    public int UserId { get; init; } = default!;
    public int PluginId { get; init; } = default!;
}
