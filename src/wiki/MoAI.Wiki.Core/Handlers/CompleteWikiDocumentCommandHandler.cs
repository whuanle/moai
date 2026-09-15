using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Storage.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Services;
using System.Transactions;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="CompleteWikiDocumentCommand"/>
/// </summary>
public class CompleteWikiDocumentCommandHandler : IRequestHandler<CompleteWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;
    private readonly WikiDocumentProcessingService _processingService;
    private readonly ILogger<CompleteWikiDocumentCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="processingService">文档内容处理服务（上传后自动提取内容入库）.</param>
    /// <param name="logger">日志.</param>
    public CompleteWikiDocumentCommandHandler(
        DatabaseContext databaseContext,
        IStorageService storageService,
        ITeamService teamService,
        IUserContextProvider userContextProvider,
        WikiDocumentProcessingService processingService,
        ILogger<CompleteWikiDocumentCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
        _processingService = processingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CompleteWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.WikiId, cancellationToken);

        int documentId;
        using (var transactionScope = TransactionScopeHelper.Create())
        {
            var objectKey = await _storageService.CompleteAsync(request.FileId, request.IsSuccess, cancellationToken);

            if (!request.IsSuccess)
            {
                transactionScope.Complete();
                return EmptyCommandResponse.Default;
            }

            var fileEntity = await _databaseContext.Files
                .FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);
            if (fileEntity == null)
            {
                throw new BusinessException("上传文件出错.") { StatusCode = 404 };
            }

            // 防御：文件必须属于该知识库（ObjectKey 由预上传按 wiki/{wikiId} 前缀生成），防止跨知识库 FileId 被借用.
            if (!fileEntity.ObjectKey.StartsWith($"wiki/{request.WikiId}/", StringComparison.Ordinal))
            {
                throw new BusinessException("上传文件出错.") { StatusCode = 404 };
            }

            var documentFile = await _databaseContext.WikiDocuments.AddAsync(new WikiDocumentEntity
            {
                WikiId = (int)request.WikiId,
                FileId = (int)request.FileId,
                FileName = request.FileName,
                ObjectKey = fileEntity.ObjectKey,
                FileType = Path.GetExtension(request.FileName),
            }, cancellationToken);

            await _databaseContext.SaveChangesAsync(cancellationToken);

            transactionScope.Complete();
            documentId = documentFile.Entity.Id;
        }

        // 上传成功后自动提取内容入库（Maomi.ToMarkdown → wiki_document_content），
        // 使文档列表/详情可直接展示内容，无需用户再到操作页手动触发提取。
        // 注意：须在事务作用域外执行（自动提取含 MinIO 读取 + 独立 SaveChanges，不应与文档登记同事务）。
        // 若个别文件类型解析失败，只记录日志、不影响文档本身创建成功；
        // 操作页的"内容提取"仍作为失败兜底的手动重试入口。
        await AutoExtractAsync((int)request.WikiId, documentId, cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task AutoExtractAsync(int wikiId, int documentId, CancellationToken cancellationToken)
    {
        try
        {
            await _processingService.ExtractAsync(wikiId, documentId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 提取失败不回滚文档创建，仅记录；用户可在文档操作页手动重试提取。
            _logger.LogWarning(ex, "上传后自动提取文档内容失败，稍后可在操作页手动重试. WikiId={WikiId}, DocumentId={DocumentId}", wikiId, documentId);
        }
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
