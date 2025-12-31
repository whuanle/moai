using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Store.Queries;
using MoAI.Wiki.Documents.Handlers;
using System.Transactions;

namespace MoAI.App.AIAssistant.Handlers.File;

/// <summary>
/// 完成上传文档.
/// </summary>
public class ComplateUploadChatFileDocumentCommandHandler : IRequestHandler<ComplateUploadChatFileDocumentCommand, ComplateUploadChatFileDocumentCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateUploadChatFileDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public ComplateUploadChatFileDocumentCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<ComplateUploadChatFileDocumentCommandResponse> Handle(ComplateUploadChatFileDocumentCommand request, CancellationToken cancellationToken)
    {
        _ = await _mediator.Send(new ComplateFileUploadCommand
        {
            FileId = request.FileId,
            IsSuccess = request.IsSuccess,
        });

        if (!request.IsSuccess)
        {
            return new ComplateUploadChatFileDocumentCommandResponse();
        }

        // 上传失败，删除数据库记录
        var fileEntity = await _databaseContext.Files.Where(x => x.Id == request.FileId).FirstOrDefaultAsync(cancellationToken);

        if (fileEntity == null)
        {
            throw new BusinessException("上传文件出错");
        }

        var fileUrl = await _mediator.Send(new QueryFileDownloadUrlCommand
        {
            ExpiryDuration = TimeSpan.FromHours(3),
            ObjectKeys = new[]
            {
                new KeyValueString
                {
                    Key = fileEntity.ObjectKey,
                    Value = Path.GetFileName(fileEntity.ObjectKey)
                }
            }
        });

        return new ComplateUploadChatFileDocumentCommandResponse
        {
            ViewUrl = fileUrl.Urls.FirstOrDefault().Value
        };
    }
}
