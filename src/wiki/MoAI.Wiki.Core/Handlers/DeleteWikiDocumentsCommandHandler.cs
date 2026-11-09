using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteWikiDocumentsCommand"/>
/// </summary>
public class DeleteWikiDocumentsCommandHandler : IRequestHandler<DeleteWikiDocumentsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;
    private readonly WikiDocumentRemovalService _removalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="removalService">文档删除领域服务（向量/切片/元数据/正文/映射/OSS/图片全链路清理）.</param>
    public DeleteWikiDocumentsCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider, WikiDocumentRemovalService removalService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
        _removalService = removalService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiDocumentsCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.WikiId, cancellationToken);

        var documentIds = request.DocumentIds.ToHashSet();
        var documents = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && documentIds.Contains(x.Id) && x.IsDeleted == 0)
            .ToArrayAsync(cancellationToken);

        if (documents.Length == 0)
        {
            throw new BusinessException("未指定要删除的文档.") { StatusCode = 404 };
        }

        await _removalService.RemoveDocumentsAsync((int)request.WikiId, documents, cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private async Task EnsureMemberAsync(long wikiId, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == wikiId && x.IsDeleted == 0, cancellationToken);
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
    }
}
