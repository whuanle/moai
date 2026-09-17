using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Settings.Services;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="PreUploadWikiDocumentCommand"/>
/// </summary>
public class PreUploadWikiDocumentCommandHandler : IRequestHandler<PreUploadWikiDocumentCommand, PreUploadWikiDocumentCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly ITeamService _teamService;
    private readonly IWikiSettingsService _wikiSettingsService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="wikiSettingsService">知识库设置服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public PreUploadWikiDocumentCommandHandler(DatabaseContext databaseContext, IStorageService storageService, ITeamService teamService, IWikiSettingsService wikiSettingsService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _teamService = teamService;
        _wikiSettingsService = wikiSettingsService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<PreUploadWikiDocumentCommandResponse> Handle(PreUploadWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.WikiId, cancellationToken);

        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrEmpty(extension) || !FileStoreHelper.DocumentFormats.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("不支持该文件格式.") { StatusCode = 400 };
        }

        var maxFileSizeMb = await _wikiSettingsService.GetMaxFileSizeMbAsync(cancellationToken);
        if (maxFileSizeMb > 0 && request.FileSize > (long)maxFileSizeMb * 1024 * 1024)
        {
            throw new BusinessException($"文件大小超过知识库上限（最大 {maxFileSizeMb} MB）.") { StatusCode = 400 };
        }

        var objectKey = FileStoreHelper.GetObjectKey(sha256: request.SHA256, fileName: request.FileName, prefix: $"wiki/{request.WikiId}");

        // 同一个知识库下不能有同 key 文件
        var existDocument = await _databaseContext.WikiDocuments
            .AnyAsync(x => x.WikiId == request.WikiId && x.ObjectKey == objectKey && x.IsDeleted == 0, cancellationToken);
        if (existDocument)
        {
            throw new BusinessException("知识库已上传过该文件.") { StatusCode = 409 };
        }

        var result = await _storageService.PreUploadAsync(new PreUploadFileCommand
        {
            SHA256 = request.SHA256,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(2)
        }, cancellationToken);

        if (result.IsExist)
        {
            var existWikiDocument = await _databaseContext.WikiDocuments
                .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.FileId == result.FileId && x.IsDeleted == 0, cancellationToken);
            if (existWikiDocument != null)
            {
                throw new BusinessException("同一个知识库下不能有相同文件.") { StatusCode = 409 };
            }
        }

        return new PreUploadWikiDocumentCommandResponse
        {
            FileId = result.FileId,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
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
