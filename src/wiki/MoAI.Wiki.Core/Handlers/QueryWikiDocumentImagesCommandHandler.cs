using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Storage.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiDocumentImagesCommand"/>
/// </summary>
public class QueryWikiDocumentImagesCommandHandler : IRequestHandler<QueryWikiDocumentImagesCommand, QueryWikiDocumentImagesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentImagesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="userInfoFillService">用户名填充服务.</param>
    public QueryWikiDocumentImagesCommandHandler(DatabaseContext databaseContext, IStorageService storageService, ITeamService teamService, IUserContextProvider userContextProvider, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentImagesCommandResponse> Handle(QueryWikiDocumentImagesCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.WikiId, cancellationToken);

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.Id == request.DocumentId && x.IsDeleted == 0, cancellationToken);
        if (document == null)
        {
            throw new BusinessException("知识库文档不存在.") { StatusCode = 404 };
        }

        var images = await _databaseContext.WikiDocumentImages
            .Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId)
            .OrderBy(x => x.ImageIndex)
            .ToListAsync(cancellationToken);

        var fileIds = images.Select(x => x.FileId).Distinct().ToArray();
        var files = await _databaseContext.Files
            .Where(x => fileIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = new List<WikiDocumentImageItem>();
        foreach (var image in images)
        {
            files.TryGetValue(image.FileId, out var file);

            // 预签名地址有效期 2 分钟，与文档下载一致
            var url = await _storageService.GetDownloadUrlAsync(image.ObjectKey, image.FileName, TimeSpan.FromMinutes(2), cancellationToken);
            items.Add(new WikiDocumentImageItem
            {
                ImageId = image.Id,
                WikiId = image.WikiId,
                DocumentId = image.DocumentId,
                FileId = image.FileId,
                FileName = image.FileName,
                FileType = image.FileType,
                FileSize = file?.FileSize ?? 0,
                PageNumber = image.PageNumber,
                ImageIndex = image.ImageIndex,
                DownloadUrl = url.ToString(),
                CreateUserId = (int)image.CreateUserId,
                CreateTime = image.CreateTime,
                UpdateTime = image.UpdateTime
            });
        }

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryWikiDocumentImagesCommandResponse
        {
            Items = items
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
