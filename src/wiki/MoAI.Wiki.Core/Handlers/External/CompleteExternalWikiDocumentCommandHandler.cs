using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;
using System.Transactions;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="CompleteExternalWikiDocumentCommand"/>
/// </summary>
public class CompleteExternalWikiDocumentCommandHandler : IRequestHandler<CompleteExternalWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly WikiDocumentProcessingService _processingService;
    private readonly ILogger<CompleteExternalWikiDocumentCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteExternalWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="processingService">文档内容处理服务（上传后自动提取内容入库）.</param>
    /// <param name="logger">日志.</param>
    public CompleteExternalWikiDocumentCommandHandler(
        DatabaseContext databaseContext,
        IStorageService storageService,
        IExternalWikiAuthorizer externalWikiAuthorizer,
        WikiDocumentProcessingService processingService,
        ILogger<CompleteExternalWikiDocumentCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _processingService = processingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CompleteExternalWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

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

            // 外部接口防御：文件必须属于该知识库（ObjectKey 由预上传按 wiki/{wikiId} 前缀生成），防止跨知识库/跨团队 FileId 被借用.
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
        // 操作页的"内容提取"仍作为失败兜底的手动重试入口（外部接口对应 extract 端点）。
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
            // 提取失败不回滚文档创建，仅记录；可再通过外部 extract 端点手动重试。
            _logger.LogWarning(ex, "上传后自动提取文档内容失败，稍后可通过 extract 接口手动重试. WikiId={WikiId}, DocumentId={DocumentId}", wikiId, documentId);
        }
    }
}
