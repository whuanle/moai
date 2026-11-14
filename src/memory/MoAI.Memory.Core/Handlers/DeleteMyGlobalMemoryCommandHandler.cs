using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteMyGlobalMemoryCommand"/>
/// </summary>
public class DeleteMyGlobalMemoryCommandHandler : IRequestHandler<DeleteMyGlobalMemoryCommand, EmptyCommandResponse>
{
    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteMyGlobalMemoryCommandHandler"/> class.
    /// </summary>
    public DeleteMyGlobalMemoryCommandHandler(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteMyGlobalMemoryCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _memoryService.DeleteGlobalAsync(request.ContextUserId, request.MemoryId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("记忆不存在或已删除.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
