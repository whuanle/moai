using DocumentFormat.OpenXml.Office2010.Word;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.ChatCompletion;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Wiki.Embedding.Commands;

namespace MoAI.Wiki.Documents.Handlers;

/// <summary>
/// 删除知识库文档.
/// </summary>
public class DeleteWikiDocumentCommandHandler : IRequestHandler<DeleteWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="aiClientBuilder"></param>
    public DeleteWikiDocumentCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions, IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
        _serviceProvider = serviceProvider;
        _aiClientBuilder = aiClientBuilder;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        var documentIds = request.DocumentIds.ToHashSet();
        if (documentIds.Count == 0)
        {
            throw new BusinessException("未指定要删除的文档") { StatusCode = 400 };
        }

        // 删除数据库记录，附属表不需要删除
        var documents = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && documentIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        if (documents.Length == 0)
        {
            throw new BusinessException("未指定要删除的文档") { StatusCode = 404 };
        }

        _databaseContext.WikiDocuments.RemoveRange(documents);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _mediator.Send(new ClearWikiDocumentEmbeddingCommand
        {
            WikiId = request.WikiId,
            DocumentIds = documents.Select(x => x.Id).ToArray(),
            IsAutoDeleteIndex = true
        });

        // 删除 oss 文件
        await _mediator.Send(new DeleteFileCommand { FileIds = documents.Select(x => x.FileId).ToArray() });

        var documentCount = await _databaseContext.WikiDocuments.Where(x => x.WikiId == request.WikiId).CountAsync();
        if (documentCount == 0)
        {
            await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsLock, false));
        }

        return EmptyCommandResponse.Default;
    }
}
