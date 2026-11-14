using System.Threading;
using System.Threading.Tasks;
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
/// <inheritdoc cref="ExtractDocumentContentCommand"/>
/// </summary>
public class ExtractDocumentContentCommandHandler : IRequestHandler<ExtractDocumentContentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;
    private readonly WikiDocumentProcessingService _processingService;
    private readonly IWikiModelMigrationService _migrationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractDocumentContentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="processingService">文档内容处理服务.</param>
    /// <param name="migrationService">模型迁移守卫（迁移期间阻断重新提取）.</param>
    public ExtractDocumentContentCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IUserContextProvider userContextProvider,
        WikiDocumentProcessingService processingService,
        IWikiModelMigrationService migrationService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
        _processingService = processingService;
        _migrationService = migrationService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ExtractDocumentContentCommand request, CancellationToken cancellationToken)
    {
        var document = await EnsureMemberAndLoadDocumentAsync(request.WikiId, request.DocumentId, cancellationToken);

        // 模型迁移期间阻断重新提取，防止内容与迁移中的向量集合错位
        await _migrationService.EnsureNotMigratingAsync(document.WikiId, cancellationToken);

        await _processingService.ExtractAsync(document.WikiId, document.Id, cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private async Task<Database.Entities.WikiDocumentEntity> EnsureMemberAndLoadDocumentAsync(long wikiId, long documentId, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == wikiId && x.IsDeleted == 0, cancellationToken);
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

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == documentId && x.WikiId == wikiId && x.IsDeleted == 0, cancellationToken);
        if (document == null)
        {
            throw new BusinessException("知识库文档不存在.") { StatusCode = 404 };
        }

        return document;
    }
}
