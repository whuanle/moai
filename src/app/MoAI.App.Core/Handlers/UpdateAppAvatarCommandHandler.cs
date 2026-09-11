using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAppAvatarCommand"/>
/// </summary>
public class UpdateAppAvatarCommandHandler : IRequestHandler<UpdateAppAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateAppAvatarCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAppAvatarCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以设置应用头像.") { StatusCode = 403 };
        }

        // 仅允许引用已完成上传并登记的文件，防止任意伪造 objectKey（与团队/知识库头像同规则）
        var fileExists = await _databaseContext.Files
            .AnyAsync(f => f.ObjectKey == request.ObjectKey && f.IsUploaded && f.IsDeleted == 0, cancellationToken);

        if (!fileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
        }

        app.Avatar = request.ObjectKey;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
