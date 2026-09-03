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
/// <inheritdoc cref="DeleteWikiDocumentCommand"/>
/// </summary>
public class DeleteWikiDocumentCommandHandler : IRequestHandler<DeleteWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public DeleteWikiDocumentCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId, cancellationToken);

        if (document == null)
        {
            throw new BusinessException("文档不存在.") { StatusCode = 404 };
        }

        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == document.WikiId, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        // 删除为破坏性操作：需要 Admin 及以上（Member 可增改但不可删）
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以删除文档.") { StatusCode = 403 };
        }

        // 实体实现 IFullAudited，框架将删除转换为软删除并记录操作人
        _databaseContext.WikiDocuments.Remove(document);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
