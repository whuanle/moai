using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateExternalAgentSessionCommand"/>
/// </summary>
public class CreateExternalAgentSessionCommandHandler : IRequestHandler<CreateExternalAgentSessionCommand, SimpleGuid>
{
    private const string DefaultTitle = "未命名标题";

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalAgentSessionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateExternalAgentSessionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateExternalAgentSessionCommand request, CancellationToken cancellationToken)
    {
        var externalUserId = ExternalAppAccessValidator.EnsureExternalUser(request.Context);

        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);
        ExternalAppAccessValidator.EnsureUsable(app);

        // 团队级授权：应用必须属于 token 归属团队
        if (app!.TeamId != request.Context.TeamId)
        {
            throw new BusinessException("该应用不属于 token 归属团队.") { StatusCode = 403 };
        }

        if (app!.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持对话.") { StatusCode = 400 };
        }

        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = app.TeamId,
            AppId = app.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? DefaultTitle : request.Title!,
            UserType = (int)UserType.External,
            LastMessageTime = DateTimeOffset.Now,
            CreateUserId = externalUserId,
        };

        _databaseContext.AppAgentSessions.Add(session);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid { Value = session.Id };
    }
}
