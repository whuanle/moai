using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamAvatarCommand"/>
/// </summary>
public class UpdateTeamAvatarCommandHandler : IRequestHandler<UpdateTeamAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateTeamAvatarCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamAvatarCommand request, CancellationToken cancellationToken)
    {
        // 仅允许引用已完成上传并登记的文件，防止任意伪造 objectKey（与用户头像同规则）
        var fileExists = await _databaseContext.Files
            .AnyAsync(f => f.ObjectKey == request.ObjectKey && f.IsUploaded && f.IsDeleted == 0, cancellationToken);

        if (!fileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
        }

        var team = await _databaseContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        team.AvatarPath = request.ObjectKey;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
