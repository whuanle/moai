using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Services;
using MoAI.Team.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteMyMemoryCommand"/>
/// </summary>
public class DeleteMyMemoryCommandHandler : IRequestHandler<DeleteMyMemoryCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryService _memoryService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteMyMemoryCommandHandler"/> class.
    /// </summary>
    public DeleteMyMemoryCommandHandler(DatabaseContext databaseContext, IMemoryService memoryService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _memoryService = memoryService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteMyMemoryCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var deleted = await _memoryService.DeleteAsync(request.AppId, request.MemoryId, request.ContextUserId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("记忆不存在或已删除.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
