using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAppCommand"/>
/// </summary>
public class CreateAppCommandHandler : IRequestHandler<CreateAppCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateAppCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以创建应用.") { StatusCode = 403 };
        }

        var nameExist = await _databaseContext.Apps
            .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("应用名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        // 创建时若要带头像，objectKey 同样必须是已完成上传并登记的文件，防止伪造（与设置头像接口同规则）
        if (!string.IsNullOrWhiteSpace(request.Avatar))
        {
            var fileExists = await _databaseContext.Files
                .AnyAsync(f => f.ObjectKey == request.Avatar && f.IsUploaded && f.IsDeleted == 0, cancellationToken);

            if (!fileExists)
            {
                throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
            }
        }

        if (request.ClassifyId > 0)
        {
            var classifyExist = await _databaseContext.Classifies
                .AnyAsync(x => x.Id == request.ClassifyId && x.Type == ClassifyTypes.App, cancellationToken);

            if (!classifyExist)
            {
                throw new BusinessException("应用分类不存在.") { StatusCode = 404 };
            }
        }

        var app = new AppEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            AppType = (int)request.AppType,
            Avatar = request.Avatar ?? string.Empty,
            IsExternal = request.IsExternal,
            IsAuth = request.IsExternal && request.IsAuth,
            ClassifyId = request.ClassifyId,
        };

        _databaseContext.Apps.Add(app);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid
        {
            Value = app.Id
        };
    }
}
