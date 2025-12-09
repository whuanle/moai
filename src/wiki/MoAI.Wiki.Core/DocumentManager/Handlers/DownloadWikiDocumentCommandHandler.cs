using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Store.Queries;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// <inheritdoc cref="DownloadWikiDocumentCommand"/>.
/// </summary>
public class DownloadWikiDocumentCommandHandler : IRequestHandler<DownloadWikiDocumentCommand, SimpleString>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public DownloadWikiDocumentCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(DownloadWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _databaseContext.WikiDocuments
            .Where(d => d.WikiId == request.WikiId && d.Id == request.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            throw new BusinessException("未找到文档文件");
        }

        var documentFile = await _databaseContext.Files
            .Where(x => x.Id == document.FileId)
            .FirstOrDefaultAsync(cancellationToken);

        if (documentFile == null)
        {
            throw new BusinessException("未找到文档文件");
        }

        var downloadUrl = await _mediator.Send(new QueryFileDownloadUrlCommand
        {
            ExpiryDuration = TimeSpan.FromMinutes(2),
            ObjectKeys = new KeyValueString[]
            {
                new KeyValueString
                {
                    Key = documentFile.ObjectKey,
                    Value = document.FileName
                }
            },
        });

        var url = downloadUrl.Urls.FirstOrDefault().Value?.ToString() ?? string.Empty;

        return new SimpleString
        {
            Value = url
        };
    }
}
