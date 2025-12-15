#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Chunkers;
using Microsoft.KernelMemory.Handlers;
using MimeKit;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Handlers;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// 文件切片预览服务.
/// </summary>
public class WikiDocumentTextPartitionPreviewCommandCommandHandler : IRequestHandler<WikiDocumentTextPartitionPreviewCommand, WikiDocumentTextPartitionPreviewCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly KmTextExtractionHandler _textExtractionHandler;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiDocumentTextPartitionPreviewCommandCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="textExtractionHandler"></param>
    /// <param name="mediator"></param>
    public WikiDocumentTextPartitionPreviewCommandCommandHandler(DatabaseContext databaseContext, KmTextExtractionHandler textExtractionHandler, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _textExtractionHandler = textExtractionHandler;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<WikiDocumentTextPartitionPreviewCommandResponse> Handle(WikiDocumentTextPartitionPreviewCommand request, CancellationToken cancellationToken)
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

        var saveFile = await _mediator.Send(new DownloadFileCommand
        {
            FileId = documentFile.Id
        });

        var text = await _textExtractionHandler.ExtractAsync(filePath: saveFile.LocalFilePath, cancellationToken: cancellationToken);

        List<WikiDocumenChunkItem> chunks = new List<WikiDocumenChunkItem>();
        var tokerizer = await GetTokenizer(request, cancellationToken);

        var kmTextPartitioningHandler = new KmTextPartitioningHandler(tokerizer);

        var mimetype = MimeTypes.GetMimeType(saveFile.LocalFilePath);
        if (mimetype != Microsoft.KernelMemory.Pipeline.MimeTypes.MarkDown)
        {
            mimetype = Microsoft.KernelMemory.Pipeline.MimeTypes.PlainText;
        }

        var partitionItems = await kmTextPartitioningHandler.PartitionAsync(
            text: text,
            mimetype,
            request.MaxTokensPerChunk,
            request.Overlap,
            request.ChunkHeader);
        int i = 0;
        foreach (var item in partitionItems)
        {
            chunks.Add(new WikiDocumenChunkItem
            {
                Order = i,
                Text = item
            });

            i++;
        }

        var entities = new List<WikiDocumentChunkContentPreviewEntity>();

        foreach (var entity in chunks)
        {
            entities.Add(new WikiDocumentChunkContentPreviewEntity
            {
                WikiId = request.WikiId,
                DocumentId = request.DocumentId,
                SliceContent = entity.Text,
                SliceOrder = entity.Order,
                SliceLength = entity.Text.Length
            });
        }

        await _databaseContext.WikiDocumentChunkContentPreviews.Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId).ExecuteDeleteAsync();
        await _databaseContext.WikiDocumentChunkContentPreviews.AddRangeAsync(entities, cancellationToken);
        document.SliceConfig = new PlainTextChunkerOptions
        {
            MaxTokensPerChunk = request.MaxTokensPerChunk,
            Overlap = request.Overlap,
            ChunkHeader = request.ChunkHeader
        }.ToJsonString();
        _databaseContext.Update(document);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new WikiDocumentTextPartitionPreviewCommandResponse
        {
            DocumentId = request.DocumentId,
            WikiId = request.WikiId
        };
    }

    private async Task<ITextTokenizer?> GetTokenizer(WikiDocumentTextPartitionPreviewCommand request, CancellationToken cancellationToken)
    {
        var wikiEntity = await _databaseContext.Wikis
            .Where(w => w.Id == request.WikiId)
            .FirstOrDefaultAsync(cancellationToken);

        if (wikiEntity == null)
        {
            throw new BusinessException("未找到知识库");
        }

        var aiModel = await _databaseContext.AiModels
            .Where(m => m.Id == wikiEntity.EmbeddingModelId)
            .FirstOrDefaultAsync(cancellationToken);
        if (aiModel == null)
        {
            throw new BusinessException("未找到知识库使用的AI模型");
        }

        var tokerizer = MoAI.AI.Helpers.TokenizerFactory.GetTokenizerForModel(aiModel.Name);
        return tokerizer;
    }
}
