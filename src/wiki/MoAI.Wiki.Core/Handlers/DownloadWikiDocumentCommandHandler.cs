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
/// <inheritdoc cref="DownloadWikiDocumentCommand"/>
/// </summary>
public class DownloadWikiDocumentCommandHandler : IRequestHandler<DownloadWikiDocumentCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public DownloadWikiDocumentCommandHandler(DatabaseContext databaseContext, IStorageService storageService, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(DownloadWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.WikiId, cancellationToken);

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.Id == request.DocumentId && x.IsDeleted == 0, cancellationToken);

        if (document == null)
        {
            throw new BusinessException("未找到文档文件.") { StatusCode = 404 };
        }

        var documentFile = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.Id == document.FileId, cancellationToken);

        if (documentFile == null)
        {
            throw new BusinessException("未找到文档文件.") { StatusCode = 404 };
        }

        var url = await _storageService.GetDownloadUrlAsync(documentFile.ObjectKey, document.FileName, TimeSpan.FromMinutes(2), cancellationToken);

        return new SimpleString
        {
            Value = url.ToString()
        };
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
