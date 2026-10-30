using MediatR;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="ClearAllMemoryCommand"/>
/// 目标保护：仅超级管理员可调用（角色门禁在 Controller）.
/// </summary>
public class ClearAllMemoryCommandHandler : IRequestHandler<ClearAllMemoryCommand, EmptyCommandResponse>
{
    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearAllMemoryCommandHandler"/> class.
    /// </summary>
    public ClearAllMemoryCommandHandler(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ClearAllMemoryCommand request, CancellationToken cancellationToken)
    {
        await _memoryService.ClearAllAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
