using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateWikiCommand"/>
/// </summary>
public class UpdateWikiCommandHandler : IRequestHandler<UpdateWikiCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public UpdateWikiCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以修改知识库.") { StatusCode = 403 };
        }

        if (wiki.Name != request.Name)
        {
            var nameExist = await _databaseContext.Wikis
                .AnyAsync(x => x.TeamId == wiki.TeamId && x.Name == request.Name, cancellationToken);

            if (nameExist)
            {
                throw new BusinessException("知识库名称已存在，请更换后重试.") { StatusCode = 409 };
            }
        }

        wiki.Name = request.Name;
        wiki.Description = request.Description ?? string.Empty;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
