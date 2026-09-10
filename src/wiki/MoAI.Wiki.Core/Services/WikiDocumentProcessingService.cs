using Maomi;
using Maomi.ToMarkdown;
using Maomi.ToMarkdown.TextSplit;
using Maomi.ToMarkdown.Token;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Services;
using MoAI.Storage.Services;
using MoAI.Wiki.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库文档内容处理：内容提取（Maomi.ToMarkdown）与文档切割（普通/AI 切割）.
/// 与 <see cref="WikiEmbeddingService"/>（向量化）解耦：先提取内容入库，再切割生成切片，最后才可向量化.
/// </summary>
[InjectOnScoped]
public class WikiDocumentProcessingService
{
    private const string DefaultPartitionPrompt = @"你是一个专业的中文知识库文档拆分助手。

请根据用户提供的完整文档内容按照以下要求拆分文本：

1. 每个文本块长度尽量不超过 1000 个字符，尽可能不要切开多个文本块，可根据语义适当调整，如果长度够用，则请勿拆分多个块。
2. 在有多个文本块的情况下，则相邻文本块需要保留约 50 个字符的重叠内容以保证上下文衔接，只有一个块则不需要生成重叠内容。
3. 尽可能不要拆开代码或段落，尽可能让语义相近的内容在一个段落内。
4. 只允许引用原文内容，不要编造或总结。
5. 输出统一使用 JSON 字符串数组，格式如下：

[
""第一块原文内容"",
""第二块原文内容""
]

只输出 JSON，不要附加其他解释。";

    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly TextExtractionService _textExtractionService;
    private readonly IWikiEmbeddingVectorStore _vectorStore;
    private readonly IIdProvider _idProvider;
    private readonly ILogger<WikiDocumentProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiDocumentProcessingService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="textExtractionService">文本抽取服务.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="idProvider">雪花 id 提供者.</param>
    /// <param name="logger">日志.</param>
    public WikiDocumentProcessingService(
        DatabaseContext databaseContext,
        IStorageService storageService,
        IChatClientProvider chatClientProvider,
        TextExtractionService textExtractionService,
        IWikiEmbeddingVectorStore vectorStore,
        IIdProvider idProvider,
        ILogger<WikiDocumentProcessingService> logger)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _chatClientProvider = chatClientProvider;
        _textExtractionService = textExtractionService;
        _vectorStore = vectorStore;
        _idProvider = idProvider;
        _logger = logger;
    }

    /// <summary>
    /// 提取文档内容：从存储读取原始文件，用 Maomi.ToMarkdown 抽取 markdown 并 upsert 到 wiki_document_content.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>抽取出的 markdown 内容.</returns>
    public async Task<string> ExtractAsync(int wikiId, int documentId, CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(wikiId, documentId, cancellationToken);
        var markdown = await ExtractMarkdownAsync(document, cancellationToken);

        var content = await _databaseContext.WikiDocumentContents
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.WikiId == wikiId, cancellationToken);
        if (content == null)
        {
            content = new WikiDocumentContentEntity
            {
                Id = _idProvider.NextId(),
                WikiId = wikiId,
                DocumentId = documentId,
                Content = markdown,
            };
            _databaseContext.WikiDocumentContents.Add(content);
        }
        else
        {
            content.Content = markdown;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return markdown;
    }

    /// <summary>
    /// 普通切割：读取已提取的内容，按 Maomi.ToMarkdown TextSplit 配置切分，替换文档切片.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="chunkSize">切片大小（>0，单位由 <paramref name="sizeUnit"/> 决定）.</param>
    /// <param name="chunkOverlap">切片重叠大小（>=0，单位由 <paramref name="overlapUnit"/> 决定）.</param>
    /// <param name="splitMode">切割模式.</param>
    /// <param name="overlapUnit">重叠单位.</param>
    /// <param name="sizeUnit">切片大小计量单位.</param>
    /// <param name="tokenEncodingOrModel">Token 计量时使用的编码名或模型名.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>生成的切片数量.</returns>
    public async Task<int> PartitionAsync(
        int wikiId,
        int documentId,
        int chunkSize,
        int chunkOverlap,
        DocumentPartitionSplitMode splitMode = DocumentPartitionSplitMode.Markdown,
        DocumentPartitionOverlapUnit overlapUnit = DocumentPartitionOverlapUnit.Character,
        DocumentPartitionSizeUnit sizeUnit = DocumentPartitionSizeUnit.Character,
        string? tokenEncodingOrModel = null,
        CancellationToken cancellationToken = default)
    {
        if (chunkSize <= 0)
        {
            throw new InvalidOperationException("切片大小必须大于 0.");
        }

        if (chunkOverlap < 0)
        {
            throw new InvalidOperationException("切片重叠不能小于 0.");
        }

        if (overlapUnit == DocumentPartitionOverlapUnit.Character && chunkOverlap >= chunkSize)
        {
            throw new InvalidOperationException("按字符重叠时，切片重叠必须小于切片大小.");
        }

        var document = await LoadDocumentAsync(wikiId, documentId, cancellationToken);
        var markdown = await LoadContentAsync(document, cancellationToken);
        var textChunks = SplitText(markdown, chunkSize, chunkOverlap, splitMode, overlapUnit, sizeUnit, tokenEncodingOrModel);

        var slices = textChunks.Select(x => x.Text).ToList();
        var sliceConfig = JsonSerializer.Serialize(new
        {
            splitMode = ToConfigValue(splitMode),
            chunkSize,
            chunkOverlap,
            overlapUnit = ToConfigValue(overlapUnit),
            sizeUnit = ToConfigValue(sizeUnit),
            tokenEncodingOrModel = sizeUnit == DocumentPartitionSizeUnit.Token ? NormalizeTokenEncodingOrModel(tokenEncodingOrModel) : null,
        });
        await ReplaceChunksAsync(document, slices, sliceConfig, cancellationToken);
        return slices.Count;
    }

    /// <summary>
    /// AI 智能切割：读取已提取的内容，调用对话模型按语义输出 JSON 字符串数组，替换文档切片.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="aiModelId">对话模型 id.</param>
    /// <param name="promptTemplate">提示词模板，为空使用内置默认.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>生成的切片数量.</returns>
    public async Task<int> AiPartitionAsync(int wikiId, int documentId, Guid aiModelId, string? promptTemplate, CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(wikiId, documentId, cancellationToken);
        var markdown = await LoadContentAsync(document, cancellationToken);

        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == wikiId && x.IsDeleted == 0, cancellationToken)
            ?? throw new InvalidOperationException($"知识库不存在: {wikiId}");

        var (model, channel) = await ResolveChatAsync(aiModelId, wiki.TeamId, cancellationToken);
        var chatClient = await _chatClientProvider.GetChatClientAsync(model, channel, cancellationToken);

        var prompt = string.IsNullOrWhiteSpace(promptTemplate) ? DefaultPartitionPrompt : promptTemplate!;
        var response = await chatClient.GetResponseAsync($"{prompt}\n\n文档内容：\n{markdown}", cancellationToken: cancellationToken);
        var text = response.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("AI 未返回有效的切割结果.");
        }

        var slices = ParseJsonChunks(text);
        if (slices.Count == 0)
        {
            throw new InvalidOperationException("AI 未返回有效的切割结果.");
        }

        await ReplaceChunksAsync(document, slices, JsonSerializer.Serialize(new { aiModelId, mode = "ai" }), cancellationToken);
        return slices.Count;
    }

    private async Task<WikiDocumentEntity> LoadDocumentAsync(int wikiId, int documentId, CancellationToken cancellationToken)
    {
        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == documentId && x.WikiId == wikiId && x.IsDeleted == 0, cancellationToken);
        if (document == null)
        {
            throw new InvalidOperationException($"知识库文档不存在: {documentId}");
        }

        return document;
    }

    private async Task<string> LoadContentAsync(WikiDocumentEntity document, CancellationToken cancellationToken)
    {
        var content = await _databaseContext.WikiDocumentContents
            .FirstOrDefaultAsync(x => x.DocumentId == document.Id && x.WikiId == document.WikiId && x.IsDeleted == 0, cancellationToken);
        if (content == null || string.IsNullOrWhiteSpace(content.Content))
        {
            throw new InvalidOperationException("文档尚未提取内容，请先执行内容提取.");
        }

        return content.Content;
    }

    private async Task<string> ExtractMarkdownAsync(WikiDocumentEntity document, CancellationToken cancellationToken)
    {
        var result = await _storageService.ReadAsync(document.ObjectKey, cancellationToken);
        await using (result.FileStream)
        {
            return await _textExtractionService.ExtractAsync(result.FileStream, document.FileName, cancellationToken);
        }
    }

    private static IReadOnlyList<TextChunk> SplitText(
        string markdown,
        int chunkSize,
        int chunkOverlap,
        DocumentPartitionSplitMode splitMode,
        DocumentPartitionOverlapUnit overlapUnit,
        DocumentPartitionSizeUnit sizeUnit,
        string? tokenEncodingOrModel)
    {
        var maomiOverlapUnit = ToMaomiOverlapUnit(overlapUnit);
        var sizeCounter = CreateSizeCounter(sizeUnit, tokenEncodingOrModel);
        return splitMode switch
        {
            DocumentPartitionSplitMode.Recursive => markdown.SplitRecursive(chunkSize, chunkOverlap, maomiOverlapUnit, sizeCounter: sizeCounter),
            DocumentPartitionSplitMode.FixedSize => markdown.SplitFixedSize(chunkSize, chunkOverlap, maomiOverlapUnit, sizeCounter),
            DocumentPartitionSplitMode.Sentence => markdown.SplitBySentence(chunkSize, chunkOverlap, maomiOverlapUnit, sizeCounter),
            DocumentPartitionSplitMode.Paragraph => markdown.SplitByParagraph(chunkSize, chunkOverlap, maomiOverlapUnit, sizeCounter),
            DocumentPartitionSplitMode.Markdown => markdown.SplitByMarkdown(chunkSize, chunkOverlap, maomiOverlapUnit, sizeCounter),
            _ => markdown.SplitByMarkdown(chunkSize, chunkOverlap, maomiOverlapUnit, sizeCounter),
        };
    }

    private static TiktokenSizeCounter? CreateSizeCounter(DocumentPartitionSizeUnit sizeUnit, string? tokenEncodingOrModel)
        => sizeUnit == DocumentPartitionSizeUnit.Token
            ? new TiktokenSizeCounter(NormalizeTokenEncodingOrModel(tokenEncodingOrModel))
            : null;

    private static string NormalizeTokenEncodingOrModel(string? tokenEncodingOrModel)
        => string.IsNullOrWhiteSpace(tokenEncodingOrModel) ? "cl100k_base" : tokenEncodingOrModel.Trim();

    private static OverlapUnit ToMaomiOverlapUnit(DocumentPartitionOverlapUnit overlapUnit)
        => overlapUnit switch
        {
            DocumentPartitionOverlapUnit.Sentence => OverlapUnit.Sentence,
            DocumentPartitionOverlapUnit.Paragraph => OverlapUnit.Paragraph,
            _ => OverlapUnit.Character,
        };

    private static string ToConfigValue(DocumentPartitionSplitMode splitMode)
        => splitMode switch
        {
            DocumentPartitionSplitMode.Recursive => "recursive",
            DocumentPartitionSplitMode.FixedSize => "fixedSize",
            DocumentPartitionSplitMode.Sentence => "sentence",
            DocumentPartitionSplitMode.Paragraph => "paragraph",
            _ => "markdown",
        };

    private static string ToConfigValue(DocumentPartitionOverlapUnit overlapUnit)
        => overlapUnit switch
        {
            DocumentPartitionOverlapUnit.Sentence => "sentence",
            DocumentPartitionOverlapUnit.Paragraph => "paragraph",
            _ => "character",
        };

    private static string ToConfigValue(DocumentPartitionSizeUnit sizeUnit)
        => sizeUnit == DocumentPartitionSizeUnit.Token ? "token" : "character";

    private async Task ReplaceChunksAsync(WikiDocumentEntity document, IReadOnlyList<string> slices, string sliceConfig, CancellationToken cancellationToken)
    {
        var oldChunkIds = await _databaseContext.WikiDocumentChunkContents
            .Where(x => x.DocumentId == document.Id && x.WikiId == document.WikiId && x.IsDeleted == 0)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (oldChunkIds.Count > 0)
        {
            await _databaseContext.WikiDocumentChunkMetadata
                .Where(x => oldChunkIds.Contains(x.ChunkId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _vectorStore.DeleteDocumentVectorsAsync(document.WikiId, document.Id, cancellationToken);
        await _databaseContext.WikiDocumentChunkContents
            .Where(x => x.DocumentId == document.Id && x.WikiId == document.WikiId)
            .ExecuteDeleteAsync(cancellationToken);

        var order = 0;
        foreach (var slice in slices)
        {
            if (string.IsNullOrEmpty(slice))
            {
                continue;
            }

            _databaseContext.WikiDocumentChunkContents.Add(new WikiDocumentChunkContentEntity
            {
                Id = _idProvider.NextId(),
                WikiId = document.WikiId,
                DocumentId = document.Id,
                SliceContent = slice,
                SliceOrder = order,
                SliceLength = slice.Length,
            });
            order++;
        }

        document.SliceConfig = sliceConfig;
        document.IsEmbedding = false;
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)> ResolveChatAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var pair = await ResolveModelAsync(modelId, teamId, cancellationToken);
        if (pair == null)
        {
            throw new InvalidOperationException("对话模型未授权给该团队或未启用.");
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

    private static List<string> ParseJsonChunks(string responseContent)
    {
        var content = Regex.Replace(responseContent, @"^```(json)?\s*", string.Empty, RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"\s*```$", string.Empty, RegexOptions.IgnoreCase);

        var list = new List<string>();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    list.Add(item.GetString()!);
                }
            }
        }

        return list;
    }
}
