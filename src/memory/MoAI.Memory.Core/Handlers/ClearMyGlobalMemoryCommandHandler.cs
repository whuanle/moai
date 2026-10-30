using MediatR;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="ClearMyGlobalMemoryCommand"/>
/// </summary>
public class ClearMyGlobalMemoryCommandHandler : IRequestHandler<ClearMyGlobalMemoryCommand, EmptyCommandResponse>
{
    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearMyGlobalMemoryCommandHandler"/> class.
    /// </summary>
    public ClearMyGlobalMemoryCommandHandler(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ClearMyGlobalMemoryCommand request, CancellationToken cancellationToken)
    {
        await _memoryService.ClearGlobalUserAsync(request.ContextUserId, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
