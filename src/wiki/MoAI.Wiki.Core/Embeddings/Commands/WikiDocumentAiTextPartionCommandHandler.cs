#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Queries;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Handlers;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// <inheritdoc cref="WikiDocumentAiTextPartionCommand"/>
/// </summary>
public class WikiDocumentAiTextPartionCommandHandler : IRequestHandler<WikiDocumentAiTextPartionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly KmTextExtractionHandler _textExtractionHandler;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiDocumentAiTextPartionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="textExtractionHandler"></param>
    /// <param name="mediator"></param>
    public WikiDocumentAiTextPartionCommandHandler(DatabaseContext databaseContext, KmTextExtractionHandler textExtractionHandler, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _textExtractionHandler = textExtractionHandler;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(WikiDocumentAiTextPartionCommand request, CancellationToken cancellationToken)
    {
        var teamId = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).Select(x => x.TeamId).FirstOrDefaultAsync();

        // 提取文档
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

        // 提取出文本
        var text = await _textExtractionHandler.ExtractAsync(filePath: saveFile.LocalFilePath, cancellationToken: cancellationToken);

        var promptTemplate = request.PromptTemplate;

        var aiEndpoint = await _mediator.Send(new QueryTeamAiModelToAiEndpointCommand
        {
            AiModelId = request.AiModelId,
            TeamId = teamId
        });

        var aiResponse = await _mediator.Send(new OneSimpleChatCommand
        {
            Endpoint = aiEndpoint,
            Question = text,
            Prompt = promptTemplate,
            AiModelId = request.AiModelId,
            ContextUserId = request.ContextUserId,
            Channel = "wiki_text_partition"
        });

        var partitionItems = ParsePartitionItems(aiResponse.Content);
        if (partitionItems.Count == 0)
        {
            throw new BusinessException("AI 未返回有效的切割结果");
        }

        var chunks = new List<WikiDocumenChunkItem>();
        var order = 0;
        foreach (var item in partitionItems)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            chunks.Add(new WikiDocumenChunkItem
            {
                Order = order,
                Text = item
            });

            order++;
        }

        if (chunks.Count == 0)
        {
            throw new BusinessException("未能生成有效的文本块");
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
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private static IReadOnlyCollection<string> ParsePartitionItems(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return Array.Empty<string>();
        }

        // 头部可能有 ``` 或 ```json
        // 尾部可能有 ```
        responseContent = Regex.Replace(responseContent, @"^```(json)?\s*", string.Empty, RegexOptions.IgnoreCase);
        responseContent = Regex.Replace(responseContent, @"\s*```$", string.Empty, RegexOptions.IgnoreCase);

        if (TryParseJsonChunks(responseContent, out var jsonChunks))
        {
            return jsonChunks;
        }

        return jsonChunks;
    }

    private static bool TryParseJsonChunks(string responseContent, out IReadOnlyCollection<string> chunks)
    {
        var list = new List<string>();
        chunks = list;
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    list.Add(item.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message, ex);
        }

        return false;
    }
}