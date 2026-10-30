using Maomi;
using Maomi.ToMarkdown;
using Maomi.ToMarkdown.TextExtract;
using Maomi.ToMarkdown.TextSplit;
using Maomi.ToMarkdown.Token;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Services;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;
using MoAI.Wiki.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

    /// <summary>
    /// 内嵌图片提取支持的文档扩展名（本期不含 html：库的 HtmlExtractor 会逐张真实下载外链图片）.
    /// </summary>
    private static readonly string[] ImageExtractExtensions = [".pdf", ".docx", ".xlsx", ".pptx"];

    /// <summary>
    /// 允许登记为附加文件的图片扩展名，浏览器可直接渲染；.raw/.jp2 等无法还原为标准编码的跳过.
    /// </summary>
    private static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    private static readonly Dictionary<string, string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
    };

    private static readonly MimeTypesDetection MimeTypeDetection = new();

    private static readonly Regex PdfImageNameRegex = new("^page_(\\d+)_image_\\d+$", RegexOptions.Compiled);

    private static readonly Regex ImageReferenceRegex = new("!\\[[^\\]]*\\]\\([^)]*\\)", RegexOptions.Compiled);

    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly TextExtractionService _textExtractionService;
    private readonly IEnumerable<ITextExtractor> _textExtractors;
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
    /// <param name="textExtractors">文本抽取器集合，内嵌图片提取需直接调用带图片参数的重载.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="idProvider">雪花 id 提供者.</param>
    /// <param name="logger">日志.</param>
    public WikiDocumentProcessingService(
        DatabaseContext databaseContext,
        IStorageService storageService,
        IChatClientProvider chatClientProvider,
        TextExtractionService textExtractionService,
        IEnumerable<ITextExtractor> textExtractors,
        IWikiEmbeddingVectorStore vectorStore,
        IIdProvider idProvider,
        ILogger<WikiDocumentProcessingService> logger)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _chatClientProvider = chatClientProvider;
        _textExtractionService = textExtractionService;
        _textExtractors = textExtractors;
        _vectorStore = vectorStore;
        _idProvider = idProvider;
        _logger = logger;
    }

    /// <summary>
    /// 提取文档内容：从存储读取原始文件，用 Maomi.ToMarkdown 抽取 markdown 并 upsert 到 wiki_document_content.
    /// pdf/word/excel/ppt 会同时提取内嵌图片：上传对象存储并登记到 wiki_document_image，
    /// 正文中的图片引用被剥离，避免失效路径进入切割与向量化.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>剥离图片引用后的 markdown 内容.</returns>
    public async Task<string> ExtractAsync(int wikiId, int documentId, CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(wikiId, documentId, cancellationToken);

        var (markdown, imageEntities) = await ExtractMarkdownWithImagesAsync(document, cancellationToken);
        markdown = StripImageReferences(markdown);

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

        await ReplaceDocumentImagesAsync(wikiId, documentId, imageEntities, cancellationToken);
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

    private async Task<(string Markdown, List<WikiDocumentImageEntity> Images)> ExtractMarkdownWithImagesAsync(WikiDocumentEntity document, CancellationToken cancellationToken)
    {
        var result = await _storageService.ReadAsync(document.ObjectKey, cancellationToken);
        await using (result.FileStream)
        {
            var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
            if (ImageExtractExtensions.Contains(extension))
            {
                var mimeType = MimeTypeDetection.GetFileType(document.FileName);
                var extractor = _textExtractors.LastOrDefault(e => e.SupportsMimeType(mimeType));
                if (extractor != null)
                {
                    // 图片统一落盘到临时目录再上传，用完即清理
                    var imageDirectory = Path.Combine(Path.GetTempPath(), "moai-wiki-images", Guid.CreateVersion7().ToString("N"));
                    Directory.CreateDirectory(imageDirectory);
                    try
                    {
                        var imageFiles = new List<string>();
                        var markdown = await extractor.ExtractAsync(result.FileStream, imageDirectory, imageFiles, cancellationToken);
                        var images = await ExtractImagesAsync(document, imageFiles, cancellationToken);
                        return (markdown, images);
                    }
                    finally
                    {
                        TryDeleteDirectory(imageDirectory);
                    }
                }
            }

            try
            {
                return (await _textExtractionService.ExtractAsync(result.FileStream, document.FileName, cancellationToken), new List<WikiDocumentImageEntity>());
            }
            catch (NotSupportedException)
            {
                // Maomi.ToMarkdown 不识别的扩展名（管理员在允许类型中放开的文本类文件，如 .csv/.sh/.cs）：
                // 嗅探排除二进制后按纯文本读取，与 PlainTextExtractor 行为对齐.
                return (await WikiTextFileReader.ReadAsTextAsync(result.FileStream, document.FileName, cancellationToken), new List<WikiDocumentImageEntity>());
            }
        }
    }

    private async Task<List<WikiDocumentImageEntity>> ExtractImagesAsync(WikiDocumentEntity document, List<string> imageFiles, CancellationToken cancellationToken)
    {
        var entities = new List<WikiDocumentImageEntity>();
        foreach (var imageFile in imageFiles)
        {
            try
            {
                var fileType = Path.GetExtension(imageFile).ToLowerInvariant();
                if (!SupportedImageExtensions.Contains(fileType))
                {
                    continue;
                }

                var bytes = await File.ReadAllBytesAsync(imageFile, cancellationToken);
                var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var fileName = Path.GetFileName(imageFile);
                var objectKey = FileStoreHelper.GetObjectKey(sha256: sha256, fileName: fileName, prefix: $"wiki/{document.WikiId}/image");

                // 按 objectKey（内容寻址）幂等：同一图片重复提取或跨文档复用不会产生重复对象
                var uploadResult = await _storageService.UploadStreamAsync(new UploadStreamFileCommand
                {
                    FileStream = new MemoryStream(bytes),
                    ContentType = ImageContentTypes.GetValueOrDefault(fileType, "application/octet-stream"),
                    FileSize = bytes.Length,
                    SHA256 = sha256,
                    ObjectKey = objectKey,
                }, cancellationToken);

                entities.Add(new WikiDocumentImageEntity
                {
                    WikiId = document.WikiId,
                    DocumentId = document.Id,
                    FileId = uploadResult.FileId,
                    ObjectKey = objectKey,
                    FileName = fileName,
                    FileType = fileType,
                    PageNumber = TryParsePdfPageNumber(fileName),
                    ImageIndex = entities.Count,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "知识库文档内嵌图片上传失败，已跳过: {ImageFile}", imageFile);
            }
        }

        return entities;
    }

    /// <summary>
    /// 替换文档的图片登记：软删旧记录、登记新记录，并清理不再被任何存活图片记录引用的旧文件
    /// （objectKey 按内容寻址，同一图片可能被多个文档共享，须做引用检查）.
    /// </summary>
    private async Task ReplaceDocumentImagesAsync(int wikiId, int documentId, List<WikiDocumentImageEntity> imageEntities, CancellationToken cancellationToken)
    {
        var oldImages = await _databaseContext.WikiDocumentImages
            .Where(x => x.WikiId == wikiId && x.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        if (oldImages.Count > 0)
        {
            _databaseContext.RemoveRange(oldImages);
        }

        if (imageEntities.Count > 0)
        {
            _databaseContext.WikiDocumentImages.AddRange(imageEntities);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        var oldFileIds = oldImages.Select(x => x.FileId).Distinct().ToList();
        await CleanupOrphanImageFilesAsync(oldFileIds, cancellationToken);
    }

    /// <summary>
    /// 删除文档的内嵌图片附件登记并清理孤儿文件（objectKey 按内容寻址，同一图片可能被多个文档共享，仅删无引用的）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentIds">被删除的文档 id 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public async Task DeleteDocumentImagesAsync(int wikiId, IReadOnlyCollection<int> documentIds, CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
        {
            return;
        }

        var images = await _databaseContext.WikiDocumentImages
            .Where(x => x.WikiId == wikiId && documentIds.Contains(x.DocumentId))
            .ToListAsync(cancellationToken);
        if (images.Count == 0)
        {
            return;
        }

        _databaseContext.WikiDocumentImages.RemoveRange(images);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await CleanupOrphanImageFilesAsync(images.Select(x => x.FileId).Distinct().ToList(), cancellationToken);
    }

    /// <summary>
    /// 清理不再被任何存活图片记录引用的图片文件；清理是尽力而为，失败只留孤儿对象不抛出.
    /// </summary>
    private async Task CleanupOrphanImageFilesAsync(IReadOnlyCollection<long> candidateFileIds, CancellationToken cancellationToken)
    {
        if (candidateFileIds.Count == 0)
        {
            return;
        }

        var stillReferenced = await _databaseContext.WikiDocumentImages
            .Where(x => candidateFileIds.Contains(x.FileId))
            .Select(x => x.FileId)
            .ToListAsync(cancellationToken);
        var orphans = candidateFileIds.Except(stillReferenced).ToList();
        if (orphans.Count == 0)
        {
            return;
        }

        try
        {
            await _storageService.DeleteFilesAsync(orphans, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理知识库文档孤儿图片文件失败: {FileIds}", string.Join(",", orphans));
        }
    }

    /// <summary>
    /// 剥离正文中的 markdown 图片引用（提取库写的是临时目录路径，进切片只会污染检索），并收敛多余空行.
    /// </summary>
    private static string StripImageReferences(string markdown)
    {
        if (!markdown.Contains("![", StringComparison.Ordinal))
        {
            return markdown;
        }

        var stripped = ImageReferenceRegex.Replace(markdown, string.Empty);
        return Regex.Replace(stripped, "(?:\r?\n){3,}", "\n\n");
    }

    /// <summary>
    /// 从 PDF 提取图片的命名 page_{页码}_image_{序号} 中解析页码，其余命名返回 null.
    /// </summary>
    private static int? TryParsePdfPageNumber(string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var match = PdfImageNameRegex.Match(nameWithoutExtension);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理文档图片临时目录失败: {Directory}", directory);
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
        var model = candidates.FirstOrDefault(x => x.c.TeamId == teamId) ?? candidates.FirstOrDefault(x => x.m.IsPublic);
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
