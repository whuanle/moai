using MediatR;
using MoAI.Infra.Models;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMyGlobalMemoriesCommand"/>
/// </summary>
public class QueryMyGlobalMemoriesCommandHandler : IRequestHandler<QueryMyGlobalMemoriesCommand, QueryMyGlobalMemoriesCommandResponse>
{
    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMyGlobalMemoriesCommandHandler"/> class.
    /// </summary>
    public QueryMyGlobalMemoriesCommandHandler(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public async Task<QueryMyGlobalMemoriesCommandResponse> Handle(QueryMyGlobalMemoriesCommand request, CancellationToken cancellationToken)
    {
        var settings = await _memoryService.GetPlatformSettingsAsync(cancellationToken);
        var (items, total) = await _memoryService.ListGlobalMineAsync(request.ContextUserId, request.Page, request.PageSize, cancellationToken);

        return new QueryMyGlobalMemoriesCommandResponse
        {
            Items = items,
            Total = total,
            GlobalEnabled = settings.Enabled,
        };
    }
}
