using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Apps.CommonApp.Queries;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;

namespace MoAI.App.AppCommon.Queries;

/// <summary>
/// <inheritdoc cref="CheckAppAccessCommand"/>
/// </summary>
public class CheckAppAccessCommandHandler : IRequestHandler<CheckAppAccessCommand, CheckAppAccessCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckAppAccessCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CheckAppAccessCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CheckAppAccessCommandResponse> Handle(CheckAppAccessCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId && !x.IsDisable)
            .Select(x => new { x.IsPublic, x.TeamId })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            return new CheckAppAccessCommandResponse { HasAccess = false };
        }

        // 公开应用，所有用户可访问
        if (app.IsPublic)
        {
            return new CheckAppAccessCommandResponse { HasAccess = true };
        }

        // 非公开应用，检查用户是否在应用所属团队内
        var isTeamMember = await _databaseContext.TeamUsers
            .AnyAsync(x => x.TeamId == app.TeamId && x.UserId == request.ContextUserId, cancellationToken);

        return new CheckAppAccessCommandResponse { HasAccess = isTeamMember };
    }
}
