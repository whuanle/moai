using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateMyGlobalMemoryCommand"/>
/// </summary>
public class UpdateMyGlobalMemoryCommandHandler : IRequestHandler<UpdateMyGlobalMemoryCommand, EmptyCommandResponse>
{
    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateMyGlobalMemoryCommandHandler"/> class.
    /// </summary>
    public UpdateMyGlobalMemoryCommandHandler(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateMyGlobalMemoryCommand request, CancellationToken cancellationToken)
    {
        var updated = await _memoryService.UpdateGlobalAsync(request.ContextUserId, request.MemoryId, request.Content, request.MemoryType, cancellationToken);
        if (!updated)
        {
            throw new BusinessException("记忆不存在或已删除.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
