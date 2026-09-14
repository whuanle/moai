using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAccessAppCommand"/>
/// </summary>
public class CreateAccessAppCommandHandler : IRequestHandler<CreateAccessAppCommand, CreateAccessAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateAccessAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<CreateAccessAppCommandResponse> Handle(CreateAccessAppCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用接入.") { StatusCode = 403 };
        }

        await AccessAppAuthorizedAppsValidator.ValidateAsync(_databaseContext, (int)request.TeamId, request.AppIds, cancellationToken);

        var (secret, keyPrefix) = AccessAppKeyGenerator.New();

        var entity = new AccessAppEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Key = secret,
            AppIds = request.AppIds.Distinct().ToList(),
        };

        _databaseContext.AccessApps.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateAccessAppCommandResponse
        {
            AccessAppId = entity.Id,
            Key = secret,
            KeyPrefix = keyPrefix,
        };
    }
}
