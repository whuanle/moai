using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Database.Enums;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppAccessPointCommand"/>
/// </summary>
public class SaveAppAccessPointCommandHandler : IRequestHandler<SaveAppAccessPointCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppAccessPointCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SaveAppAccessPointCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveAppAccessPointCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId, x.IsExternal })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        if (!app.IsExternal)
        {
            throw new BusinessException("只有外部应用可以配置访问点.") { StatusCode = 400 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以配置访问点.") { StatusCode = 403 };
        }

        // 头像仅允许引用已完成上传并登记的文件（与团队/应用头像同规则）
        if (!string.IsNullOrEmpty(request.Avatar))
        {
            var fileExists = await _databaseContext.Files
                .AnyAsync(f => f.ObjectKey == request.Avatar && f.IsUploaded && f.IsDeleted == 0, cancellationToken);
            if (!fileExists)
            {
                throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
            }
        }

        var config = await _databaseContext.AppAccessPoints
            .FirstOrDefaultAsync(x => x.AppId == request.AppId, cancellationToken);

        if (config == null)
        {
            config = new Database.Entities.AppAccessPointEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = app.TeamId,
                AppId = request.AppId,
            };
            _databaseContext.AppAccessPoints.Add(config);
        }

        config.Title = request.Title;
        config.Subtitle = request.Subtitle;
        config.Placeholder = request.Placeholder;
        config.PrimaryColor = string.IsNullOrEmpty(request.PrimaryColor) ? null : request.PrimaryColor;
        config.Position = request.Position.ToJsonString();
        config.LauncherText = request.LauncherText;
        config.Avatar = request.Avatar;
        config.PanelWidth = request.PanelWidth;
        config.PanelHeight = request.PanelHeight;
        config.DefaultOpen = request.DefaultOpen;
        config.Enabled = request.Enabled;

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
