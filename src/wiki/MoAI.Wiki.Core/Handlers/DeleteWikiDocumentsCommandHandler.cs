using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Storage.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteWikiDocumentsCommand"/>
/// </summary>
public class DeleteWikiDocumentsCommandHandler : IRequestHandler<DeleteWikiDocumentsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public DeleteWikiDocumentsCommandHandler(DatabaseContext databaseContext, IStorageService storageService, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
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

        _databaseContext.WikiDocuments.RemoveRange(documents);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 删除关联切片/元数据
        var deletedDocumentIds = documents.Select(x => x.Id).ToArray();
        var chunks = await _databaseContext.WikiDocumentChunkContents
            .Where(x => deletedDocumentIds.Contains(x.DocumentId))
            .ToArrayAsync(cancellationToken);
        if (chunks.Length > 0)
        {
            _databaseContext.WikiDocumentChunkContents.RemoveRange(chunks);
        }

        var metadata = await _databaseContext.WikiDocumentChunkMetadata
            .Where(x => deletedDocumentIds.Contains(x.DocumentId))
            .ToArrayAsync(cancellationToken);
        if (metadata.Length > 0)
        {
            _databaseContext.WikiDocumentChunkMetadata.RemoveRange(metadata);
        }

        // 删除 oss 文件
        var fileIds = documents.Select(x => (long)x.FileId).ToArray();
        await _storageService.DeleteFilesAsync(fileIds, cancellationToken);

        await _databaseContext.SaveChangesAsync(cancellationToken);

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
