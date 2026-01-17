using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Apps.CommonApp.Queries;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;

namespace MoAI.App.AppCommon.Queries;

/// <summary>
/// <inheritdoc cref="CheckUserAppPermissionCommand"/>
/// </summary>
public class CheckUserAppPermissionCommandHandler : IRequestHandler<CheckUserAppPermissionCommand, CheckUserAppPermissionCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckUserAppPermissionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CheckUserAppPermissionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CheckUserAppPermissionCommandResponse> Handle(CheckUserAppPermissionCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.IsPublic, x.TeamId, x.IsDisable })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            return new CheckUserAppPermissionCommandResponse
            {
                HasPermission = false,
                AppExists = false,
                IsAppDisabled = false
            };
        }

        if (app.IsDisable)
        {
            return new CheckUserAppPermissionCommandResponse
            {
                HasPermission = false,
                AppExists = true,
                IsAppDisabled = true
            };
        }

        // 公开应用，所有用户可使用
        if (app.IsPublic)
        {
            return new CheckUserAppPermissionCommandResponse
            {
                HasPermission = true,
                AppExists = true,
                IsAppDisabled = false
            };
        }

        // 非公开应用，检查用户是否在应用所属团队内
        var isTeamMember = await _databaseContext.TeamUsers
            .AnyAsync(x => x.TeamId == app.TeamId && x.UserId == request.ContextUserId, cancellationToken);

        return new CheckUserAppPermissionCommandResponse
        {
            HasPermission = isTeamMember,
            AppExists = true,
            IsAppDisabled = false
        };
    }
}
