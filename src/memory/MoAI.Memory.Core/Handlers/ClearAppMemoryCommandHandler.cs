using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Services;
using MoAI.Team.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="ClearAppMemoryCommand"/>：仅团队管理员可清空应用全部用户的记忆.
/// </summary>
public class ClearAppMemoryCommandHandler : IRequestHandler<ClearAppMemoryCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryService _memoryService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearAppMemoryCommandHandler"/> class.
    /// </summary>
    public ClearAppMemoryCommandHandler(DatabaseContext databaseContext, IMemoryService memoryService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _memoryService = memoryService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ClearAppMemoryCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null || myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以清空应用记忆.") { StatusCode = 403 };
        }

        await _memoryService.ClearAppAsync(request.AppId, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
