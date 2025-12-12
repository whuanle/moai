#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Commands;
using MoAI.AI.Models;
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

        // ai 切割
        var aiModel = await _databaseContext.AiModels
            .Where(x => x.Id == request.AiModelId && x.IsPublic)
            .FirstOrDefaultAsync();

        if (aiModel == null)
        {
            throw new BusinessException("未找到可用 ai 模型");
        }

        var aiEndpoint = new AiEndpoint
        {
            Name = aiModel.Name,
            DeploymentName = aiModel.DeploymentName,
            Title = aiModel.Title,
            AiModelType = Enum.Parse<AiModelType>(aiModel.AiModelType, true),
            Provider = Enum.Parse<AiProvider>(aiModel.AiProvider, true),
            ContextWindowTokens = aiModel.ContextWindowTokens,
            Endpoint = aiModel.Endpoint,
            Key = aiModel.Key,
            Abilities = new ModelAbilities
            {
                Files = aiModel.Files,
                FunctionCall = aiModel.FunctionCall,
                ImageOutput = aiModel.ImageOutput,
                Vision = aiModel.IsVision,
            },
            MaxDimension = aiModel.MaxDimension,
            TextOutput = aiModel.TextOutput
        };

        var aiResponse = await _mediator.Send(new OneSimpleChatCommand
        {
            Endpoint = aiEndpoint,
            Question = text,
            Prompt = promptTemplate
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

        return SplitByParagraph(responseContent);
    }

    private static bool TryParseJsonChunks(string responseContent, out IReadOnlyCollection<string> chunks)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("chunks", out var chunkElement) && chunkElement.ValueKind == JsonValueKind.Array)
                {
                    var parsed = ExtractStringChunksFromArray(chunkElement);
                    if (parsed.Count > 0)
                    {
                        chunks = parsed;
                        return true;
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                var parsed = ExtractStringChunksFromArray(root);
                if (parsed.Count > 0)
                {
                    chunks = parsed;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // ignore and fallback
        }

        chunks = Array.Empty<string>();
        return false;
    }

    private static List<string> ExtractStringChunksFromArray(JsonElement element)
    {
        var result = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(value.Trim());
                    }

                    break;
                case JsonValueKind.Object:
                    var text = ReadTextProperty(item);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(text.Trim());
                    }

                    break;
            }
        }

        return result;
    }

    private static string? ReadTextProperty(JsonElement element)
    {
        if (element.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
        {
            return textElement.GetString();
        }

        if (element.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString();
        }

        if (element.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.String)
        {
            return valueElement.GetString();
        }

        return null;
    }

    private static IReadOnlyCollection<string> SplitByParagraph(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Trim();
        var segments = Regex.Split(normalized, "\n{2,}")
            .Select(NormalizeChunkText)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count == 0 && !string.IsNullOrWhiteSpace(normalized))
        {
            segments.Add(NormalizeChunkText(normalized));
        }

        return segments;
    }

    private static string NormalizeChunkText(string chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk))
        {
            return string.Empty;
        }

        var cleaned = chunk.Trim();
        cleaned = Regex.Replace(cleaned, @"^(?:[-*•]+)\s*", string.Empty);
        cleaned = Regex.Replace(cleaned, @"^(?:chunk|part|section|段落|第)\s*\d+\s*(?:[-:：、])?\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^\d+\s*(?:[.:：、-])\s*", string.Empty);
        return cleaned.Trim();
    }
}