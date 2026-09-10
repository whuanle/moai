using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Wiki.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库文档向量化流水线：复用已提取内容 + 已切割切片，执行 可选元数据生成 → 向量化.
/// 内容提取见 <see cref="WikiDocumentProcessingService.ExtractAsync"/>，切割见其 PartitionAsync / AiPartitionAsync.
/// 向量模型/维度由知识库配置；元数据模型由本次触发任务动态传入.
/// </summary>
[InjectOnScoped]
public class WikiEmbeddingService : IWikiEmbeddingProcessor
{
    private readonly DatabaseContext _databaseContext;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly IAiChatCompletionService _chatCompletionService;
    private readonly IWikiEmbeddingVectorStore _vectorStore;
    private readonly IIdProvider _idProvider;
    private readonly ILogger<WikiEmbeddingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiEmbeddingService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="chatCompletionService">一次性对话服务.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="idProvider">雪花 id 提供者.</param>
    /// <param name="logger">日志.</param>
    public WikiEmbeddingService(
        DatabaseContext databaseContext,
        IEmbeddingGeneratorProvider embeddingGeneratorProvider,
        IAiChatCompletionService chatCompletionService,
        IWikiEmbeddingVectorStore vectorStore,
        IIdProvider idProvider,
        ILogger<WikiEmbeddingService> logger)
    {
        _databaseContext = databaseContext;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _chatCompletionService = chatCompletionService;
        _vectorStore = vectorStore;
        _idProvider = idProvider;
        _logger = logger;
    }

    /// <summary>
    /// 执行文档向量化流水线（复用已提取内容 + 已切割切片）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="isEmbedSourceText">是否对原文切片向量化.</param>
    /// <param name="isEmbedMetadata">是否对元数据向量化.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    public async Task ProcessAsync(
        int wikiId,
        int documentId,
        bool isEmbedSourceText,
        bool isEmbedMetadata,
        CancellationToken cancellationToken = default)
    {
        if (!isEmbedSourceText && !isEmbedMetadata)
        {
            throw new ArgumentException("至少需要选择一种向量化内容（原文或元数据）。", nameof(isEmbedSourceText));
        }

        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == wikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            throw new InvalidOperationException($"知识库不存在: {wikiId}");
        }

        if (wiki.EmbeddingModelId == Guid.Empty || wiki.EmbeddingDimensions <= 0)
        {
            throw new InvalidOperationException("知识库未配置向量化模型或维度.");
        }

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == documentId && x.WikiId == wikiId && x.IsDeleted == 0, cancellationToken);
        if (document == null)
        {
            throw new InvalidOperationException($"知识库文档不存在: {documentId}");
        }

        var content = await _databaseContext.WikiDocumentContents
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.WikiId == wikiId && x.IsDeleted == 0, cancellationToken);
        if (content == null || string.IsNullOrWhiteSpace(content.Content))
        {
            throw new InvalidOperationException("文档尚未提取内容，请先执行内容提取.");
        }

        var chunks = await _databaseContext.WikiDocumentChunkContents
            .Where(x => x.DocumentId == documentId && x.WikiId == wikiId && x.IsDeleted == 0)
            .OrderBy(x => x.SliceOrder)
            .ToListAsync(cancellationToken);
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("文档尚未切割，请先执行文档切割.");
        }

        var (model, channel) = await ResolveEmbeddingAsync(wiki.EmbeddingModelId, wiki.TeamId, cancellationToken);
        var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(model, channel, cancellationToken);

        var metadataByChunk = isEmbedMetadata
            ? await LoadMetadataByChunkAsync(chunks.Select(x => x.Id).ToList(), cancellationToken)
            : new Dictionary<long, List<WikiDocumentChunkMetadatumEntity>>();

        var records = BuildEmbeddingRecords(wikiId, documentId, chunks, metadataByChunk, isEmbedSourceText, isEmbedMetadata);
        if (records.Count == 0)
        {
            throw new InvalidOperationException("未构建出任何可向量化记录：请确认已启用原文向量化或文档切片存在可用元数据。");
        }

        foreach (var record in records)
        {
            record.Embedding = await GenerateEmbeddingVectorAsync(generator, wiki, record, cancellationToken);
        }

        // 先完成外部模型调用并校验，再写入向量集合，避免失败时丢失旧数据。
        await _vectorStore.EnsureCollectionAsync(wikiId, wiki.EmbeddingDimensions, cancellationToken);
        await _vectorStore.ReplaceDocumentVectorsAsync(wikiId, documentId, records, cancellationToken);

        document.IsEmbedding = true;
        wiki.IsLock = true;
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    internal static List<WikiEmbeddingVectorRecord> BuildEmbeddingRecords(
        int wikiId,
        int documentId,
        IReadOnlyCollection<WikiDocumentChunkContentEntity> chunks,
        IReadOnlyDictionary<long, List<WikiDocumentChunkMetadatumEntity>> metadataByChunk,
        bool isEmbedSourceText,
        bool isEmbedMetadata)
    {
        if (!isEmbedSourceText && !isEmbedMetadata)
        {
            throw new ArgumentException("至少需要选择一种向量化内容（原文或元数据）。", nameof(isEmbedSourceText));
        }

        var records = new List<WikiEmbeddingVectorRecord>();
        foreach (var chunk in chunks)
        {
            if (isEmbedSourceText)
            {
                records.Add(new WikiEmbeddingVectorRecord
                {
                    Key = Guid.CreateVersion7(),
                    WikiId = wikiId,
                    DocumentId = documentId,
                    ChunkId = chunk.Id,
                    MetadataType = 0,
                    Content = chunk.SliceContent,
                });
            }

            if (isEmbedMetadata && metadataByChunk.TryGetValue(chunk.Id, out var metadatas))
            {
                foreach (var metadata in metadatas)
                {
                    records.Add(new WikiEmbeddingVectorRecord
                    {
                        Key = Guid.CreateVersion7(),
                        WikiId = wikiId,
                        DocumentId = documentId,
                        ChunkId = chunk.Id,
                        MetadataType = metadata.MetadataType,
                        Content = metadata.MetadataContent,
                    });
                }
            }
        }

        return records;
    }

    /// <summary>
    /// 为指定文档切片生成并保存元数据；未指定切片时处理文档全部切片.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="metadataModelId">元数据生成使用的对话模型 id.</param>
    /// <param name="chunkIds">目标切片 id；为空时处理全部切片.</param>
    /// <param name="appendExisting">是否保留已有元数据并追加生成结果.</param>
    /// <param name="strategyType">元数据生成策略；为空时生成全套元数据.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>生成的元数据数量.</returns>
    internal async Task<int> GenerateAndSaveChunkMetadataAsync(int wikiId, int documentId, Guid metadataModelId, List<long> chunkIds, bool appendExisting = false, MetadataGenerationStrategy? strategyType = null, CancellationToken cancellationToken = default)
    {
        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == wikiId && x.IsDeleted == 0, cancellationToken)
            ?? throw new InvalidOperationException($"知识库不存在: {wikiId}");
        var document = await _databaseContext.WikiDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.WikiId == wikiId && x.IsDeleted == 0, cancellationToken)
            ?? throw new InvalidOperationException($"知识库文档不存在: {documentId}");

        var query = _databaseContext.WikiDocumentChunkContents
            .Where(x => x.DocumentId == documentId && x.WikiId == wikiId && x.IsDeleted == 0);
        if (chunkIds.Count > 0)
        {
            query = query.Where(x => chunkIds.Contains(x.Id));
        }

        var chunks = await query.OrderBy(x => x.SliceOrder).ToListAsync(cancellationToken);
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("文档尚未切割，请先执行文档切割.");
        }

        var selectedChunkIds = chunks.Select(x => x.Id).ToList();
        if (!appendExisting)
        {
            await _databaseContext.WikiDocumentChunkMetadata
                .Where(x => selectedChunkIds.Contains(x.ChunkId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var metadataByChunk = new Dictionary<long, List<WikiDocumentChunkMetadatumEntity>>();
        var failures = await GenerateMetadataAsync(wiki, document, metadataModelId, chunks, metadataByChunk, strategyType, cancellationToken);
        var generatedCount = metadataByChunk.Values.Sum(x => x.Count);
        if (generatedCount == 0)
        {
            var lastFailure = failures.LastOrDefault();
            if (lastFailure != null)
            {
                throw new BusinessException($"元数据生成失败：{lastFailure.Message}")
                {
                    StatusCode = lastFailure.Message.Contains("429", StringComparison.Ordinal) ? 429 : 502,
                };
            }

            throw new BusinessException("元数据生成失败：模型未返回有效内容.") { StatusCode = 400 };
        }

        return generatedCount;
    }

    private async Task<List<Exception>> GenerateMetadataAsync(
        WikiEntity wiki,
        WikiDocumentEntity document,
        Guid metadataModelId,
        List<WikiDocumentChunkContentEntity> chunks,
        Dictionary<long, List<WikiDocumentChunkMetadatumEntity>> metadataByChunk,
        MetadataGenerationStrategy? strategyType,
        CancellationToken cancellationToken)
    {
        var (model, channel) = await ResolveChatAsync(metadataModelId, wiki.TeamId, cancellationToken);
        var failures = new List<Exception>();

        foreach (var chunk in chunks)
        {
            try
            {
                var metadata = await GenerateChunkMetadataAsync(model, channel, chunk.SliceContent, strategyType, cancellationToken);
                if (metadata == null)
                {
                    continue;
                }

                var entities = new List<WikiDocumentChunkMetadatumEntity>();
                if (!string.IsNullOrWhiteSpace(metadata.Outline))
                {
                    entities.Add(CreateMetadata(wiki.Id, document.Id, chunk.Id, 1, metadata.Outline));
                }

                foreach (var question in metadata.Questions)
                {
                    entities.Add(CreateMetadata(wiki.Id, document.Id, chunk.Id, 2, question));
                }

                foreach (var keyword in metadata.Keywords)
                {
                    entities.Add(CreateMetadata(wiki.Id, document.Id, chunk.Id, 3, keyword));
                }

                if (!string.IsNullOrWhiteSpace(metadata.Summary))
                {
                    entities.Add(CreateMetadata(wiki.Id, document.Id, chunk.Id, 4, metadata.Summary));
                }

                if (!string.IsNullOrWhiteSpace(metadata.Aggregated))
                {
                    entities.Add(CreateMetadata(wiki.Id, document.Id, chunk.Id, 5, metadata.Aggregated));
                }

                if (entities.Count > 0)
                {
                    await _databaseContext.WikiDocumentChunkMetadata.AddRangeAsync(entities, cancellationToken);
                    metadataByChunk[chunk.Id] = entities;
                }
            }
            catch (Exception ex)
            {
                failures.Add(ex);
                _logger.LogError(ex, "生成切片元数据失败. DocumentId={DocumentId}, ChunkId={ChunkId}", document.Id, chunk.Id);
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return failures;
    }

    private async Task<Dictionary<long, List<WikiDocumentChunkMetadatumEntity>>> LoadMetadataByChunkAsync(List<long> chunkIds, CancellationToken cancellationToken)
    {
        if (chunkIds.Count == 0)
        {
            return new Dictionary<long, List<WikiDocumentChunkMetadatumEntity>>();
        }

        var metadatas = await _databaseContext.WikiDocumentChunkMetadata
            .Where(x => chunkIds.Contains(x.ChunkId) && x.IsDeleted == 0)
            .ToListAsync(cancellationToken);
        return metadatas
            .GroupBy(x => x.ChunkId)
            .ToDictionary(x => x.Key, x => x.ToList());
    }

    private async Task<ChunkMetadata?> GenerateChunkMetadataAsync(AiModelEntity model, AiChannelEntity channel, string content, MetadataGenerationStrategy? strategyType, CancellationToken cancellationToken)
    {
        var prompt = strategyType switch
        {
            MetadataGenerationStrategy.OutlineGeneration => $@"
/no_think
请关闭深度思考，直接阅读以下文档切片，提取简洁结构化大纲，并严格返回 JSON（不要返回其他内容）：
{{
    ""outline"": ""该切片的大纲/主题""
}}

文档切片：
{content}",
            MetadataGenerationStrategy.QuestionGeneration => $@"
/no_think
请关闭深度思考，直接阅读以下文档切片，生成该切片可以回答的核心问题，并严格返回 JSON（不要返回其他内容）：
{{
    ""questions"": [""该切片可以回答的问题1"", ""问题2""]
}}

文档切片：
{content}",
            MetadataGenerationStrategy.KeywordSummaryFusion => $@"
/no_think
请关闭深度思考，直接阅读以下文档切片，提取关键词并生成摘要，并严格返回 JSON（不要返回其他内容）：
{{
    ""keywords"": [""关键词1"", ""关键词2""],
    ""summary"": ""该切片的摘要""
}}

文档切片：
{content}",
            MetadataGenerationStrategy.SemanticAggregation => $@"
/no_think
请关闭深度思考，直接阅读以下文档切片，将核心语义聚合为一个简洁片段，并严格返回 JSON（不要返回其他内容）：
{{
    ""aggregated"": ""该切片的核心语义聚合结果""
}}

文档切片：
{content}",
                        _ => $@"
/no_think
请关闭深度思考，直接阅读以下文档切片，提取大纲、问题、关键词和摘要，并严格返回 JSON（不要返回其他内容）：
{{
  ""outline"": ""该切片的大纲/主题"",
  ""questions"": [""该切片可以回答的问题1"", ""问题2""],
  ""keywords"": [""关键词1"", ""关键词2""],
  ""summary"": ""该切片的摘要""
}}

文档切片：
{content}",
    };

        var text = (await _chatCompletionService.CompleteTextAsync(
            model,
            channel,
            prompt,
            new AiChatCompletionOptions
            {
                DisableThinking = true,
                MaxOutputTokens = 512,
                PreferJsonResponse = true,
                Temperature = 0,
            },
            cancellationToken)).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return ParseChunkMetadata(text, strategyType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析切片元数据 JSON 失败. Text={Text}", text);
            return ParsePlainTextMetadata(text, strategyType);
        }
    }

    private static ChunkMetadata ParseChunkMetadata(string text, MetadataGenerationStrategy? strategyType)
    {
        var metadata = new ChunkMetadata();
        using var document = JsonDocument.Parse(ExtractJson(text));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ParsePlainTextMetadata(text, strategyType);
        }

        metadata.Outline = ReadString(root, "outline", "title", "topic");
        metadata.Questions = ReadStringList(root, ["questions", "question", "queries", "query"], splitText: true);
        metadata.Keywords = ReadStringList(root, ["keywords", "keyword", "keyWords", "tags"], splitText: true);
        metadata.Summary = ReadString(root, "summary", "abstract", "description");
        metadata.Aggregated = ReadString(root, "aggregated", "aggregatedSubParagraph", "semanticAggregation", "processedText", "content", "text");

        return HasMetadata(metadata) ? metadata : ParsePlainTextMetadata(text, strategyType);
    }

    private static ChunkMetadata ParsePlainTextMetadata(string text, MetadataGenerationStrategy? strategyType)
    {
        var content = text.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ChunkMetadata();
        }

        return strategyType switch
        {
            MetadataGenerationStrategy.QuestionGeneration => new ChunkMetadata { Questions = ReadLines(content) },
            MetadataGenerationStrategy.KeywordSummaryFusion => new ChunkMetadata { Summary = content },
            MetadataGenerationStrategy.SemanticAggregation => new ChunkMetadata { Aggregated = content },
            _ => new ChunkMetadata { Outline = content },
        };
    }

    private static bool HasMetadata(ChunkMetadata metadata)
    {
        return !string.IsNullOrWhiteSpace(metadata.Outline)
            || metadata.Questions.Count > 0
            || metadata.Keywords.Count > 0
            || !string.IsNullOrWhiteSpace(metadata.Summary)
            || !string.IsNullOrWhiteSpace(metadata.Aggregated);
    }

    private static string ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString()?.Trim() ?? string.Empty;
                }

                if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return value.ToString().Trim();
                }
            }
        }

        return string.Empty;
    }

    private static List<string> ReadStringList(JsonElement root, string[] names, bool splitText)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .ToList();
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString() ?? string.Empty;
                return splitText ? SplitListText(text) : [text.Trim()];
            }
        }

        return [];
    }

    private static List<string> SplitListText(string text)
    {
        return text.Split([',', '，', ';', '；', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static List<string> ReadLines(string text)
    {
        return text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingVectorAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        WikiEntity wiki,
        WikiEmbeddingVectorRecord record,
        CancellationToken cancellationToken)
    {
        var embeddings = await generator.GenerateAsync(
            [record.Content],
            options: new EmbeddingGenerationOptions { Dimensions = wiki.EmbeddingDimensions },
            cancellationToken: cancellationToken);

        if (embeddings.Count == 0)
        {
            throw new InvalidOperationException($"向量模型未返回向量：ChunkId={record.ChunkId}, MetadataType={record.MetadataType}。");
        }

        var vector = embeddings[0].Vector;
        if (vector.Length == 0)
        {
            throw new InvalidOperationException($"向量模型返回空向量：ChunkId={record.ChunkId}, MetadataType={record.MetadataType}。");
        }

        return vector;
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)> ResolveEmbeddingAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var pair = await ResolveModelAsync(modelId, teamId, cancellationToken);
        if (pair == null)
        {
            throw new InvalidOperationException("向量化模型未授权给该团队或未启用.");
        }

        return pair.Value;
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)> ResolveChatAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var pair = await ResolveModelAsync(modelId, teamId, cancellationToken);
        if (pair == null)
        {
            throw new InvalidOperationException("元数据生成模型未授权给该团队或未启用.");
        }

        return pair.Value;
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                    select new { m, c };

        var candidates = await query.ToListAsync(cancellationToken);
        var model = candidates.FirstOrDefault(x => x.m.IsPublic);
        if (model == null)
        {
            var authorized = await _databaseContext.AiModelAuthorizations
                .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
            if (authorized)
            {
                model = candidates.FirstOrDefault();
            }
        }

        return model == null ? null : (model.m, model.c);
    }

    private WikiDocumentChunkMetadatumEntity CreateMetadata(int wikiId, int documentId, long chunkId, int metadataType, string content)
    {
        return new WikiDocumentChunkMetadatumEntity
        {
            Id = _idProvider.NextId(),
            WikiId = wikiId,
            DocumentId = documentId,
            ChunkId = chunkId,
            MetadataType = metadataType,
            MetadataContent = content,
        };
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return text[start..(end + 1)];
        }

        return text;
    }

    private sealed class ChunkMetadata
    {
        public string Outline { get; set; } = string.Empty;

        public List<string> Questions { get; set; } = new();

        public List<string> Keywords { get; set; } = new();

        public string Summary { get; set; } = string.Empty;

        public string Aggregated { get; set; } = string.Empty;
    }
}
