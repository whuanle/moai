using DocumentFormat.OpenXml.Office2010.Word;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// 删除 wiki.
/// </summary>
public class DeleteWikiCommandHandler : IRequestHandler<DeleteWikiCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public DeleteWikiCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).FirstOrDefaultAsync();
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        // 清空向量
        await _mediator.Send(new ClearWikiDocumentEmbeddingCommand
        {
            WikiId = request.WikiId,
            IsAutoDeleteIndex = true,
        });

        var fileIds = await _databaseContext.WikiDocuments.Where(x => x.WikiId == request.WikiId)
            .Select(x => x.FileId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // 删除 oss 文件
        await _mediator.Send(new DeleteFileCommand { FileIds = fileIds });

        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId));

        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocumentChunkContentPreviews
            .Where(x => x.WikiId == request.WikiId));

        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocumentChunkMetadataPreviews
            .Where(x => x.WikiId == request.WikiId));

        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocumentChunkEmbeddings
            .Where(x => x.WikiId == request.WikiId));

        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigDocuments
            .Where(x => x.WikiId == request.WikiId));

        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigDocumentStates
            .Where(x => x.WikiId == request.WikiId));

        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigs
            .Where(x => x.WikiId == request.WikiId));

        _databaseContext.Remove(wiki);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}