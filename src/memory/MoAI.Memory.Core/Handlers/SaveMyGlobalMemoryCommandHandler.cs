using MediatR;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Models;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="SaveMyGlobalMemoryCommand"/>
/// </summary>
public class SaveMyGlobalMemoryCommandHandler : IRequestHandler<SaveMyGlobalMemoryCommand, MemoryItem>
{
    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveMyGlobalMemoryCommandHandler"/> class.
    /// </summary>
    public SaveMyGlobalMemoryCommandHandler(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public Task<MemoryItem> Handle(SaveMyGlobalMemoryCommand request, CancellationToken cancellationToken)
        => _memoryService.SaveGlobalAsync(request.ContextUserId, request.Content, request.MemoryType, cancellationToken);
}
