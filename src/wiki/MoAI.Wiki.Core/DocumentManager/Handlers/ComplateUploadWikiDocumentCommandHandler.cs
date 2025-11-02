using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using System.Transactions;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// 完成上传文档.
/// </summary>
public class ComplateUploadWikiDocumentCommandHandler : IRequestHandler<ComplateUploadWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateUploadWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public ComplateUploadWikiDocumentCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ComplateUploadWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        _ = await _mediator.Send(new ComplateFileUploadCommand
        {
            FileId = request.FileId,
            IsSuccess = request.IsSuccess,
        });

        if (!request.IsSuccess)
        {
            return EmptyCommandResponse.Default;
        }

        // 上传失败，删除数据库记录
        var fileEntity = await _databaseContext.Files.Where(x => x.Id == request.FileId).FirstOrDefaultAsync(cancellationToken);

        if (fileEntity == null)
        {
            throw new BusinessException("上传文件出错");
        }

        var documentFile = await _databaseContext.WikiDocuments.AddAsync(new Database.Entities.WikiDocumentEntity
        {
            FileId = request.FileId,
            WikiId = request.WikiId,
            FileName = request.FileName,
            ObjectKey = fileEntity.ObjectKey,
            FileType = Path.GetExtension(request.FileName)
        });

        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
