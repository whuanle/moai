using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Services;
using MoAI.Storage.Commands;
using MoAI.Storage.Services;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 外部源内容落库器：把一次抓取/拉取得到的内容写入知识库文档（文件上传 → 文档登记 → 内容入库），
/// 并触发外部源工作流；供飞书文档同步与网页爬虫共用，保证两类外部源行为一致.
/// </summary>
[InjectOnScoped]
public class WikiSourceContentWriter
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IIdProvider _idProvider;
    private readonly WikiSourceWorkflowRunner _workflowRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSourceContentWriter"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">文件存储领域服务.</param>
    /// <param name="idProvider">雪花 id 提供者.</param>
    /// <param name="workflowRunner">外部源工作流执行器.</param>
    public WikiSourceContentWriter(
        DatabaseContext databaseContext,
        IStorageService storageService,
        IIdProvider idProvider,
        WikiSourceWorkflowRunner workflowRunner)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _idProvider = idProvider;
        _workflowRunner = workflowRunner;
    }

    /// <summary>
    /// 写入或更新单个外部文档.
    /// </summary>
    /// <param name="wiki">知识库实体.</param>
    /// <param name="source">外部源实体.</param>
    /// <param name="item">待写入内容.</param>
    /// <param name="mapping">已存在的映射记录，null 表示新建.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文档 id、后台任务 id 与结果说明.</returns>
    public async Task<(long DocumentId, Guid? TaskId, string Message)> WriteAsync(
        WikiEntity wiki,
        WikiSourceEntity source,
        WikiSourceContentItem item,
        WikiSourceDocumentEntity? mapping,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(item.Content);
        var objectKey = $"wiki/{wiki.Id}/source/{source.Id}/{SafeObjectKey(item.ExternalKey)}.md";
        var fileName = SafeFileName(item.Title) + ".md";

        var document = mapping != null && mapping.DocumentId > 0
            ? await _databaseContext.WikiDocuments.FirstOrDefaultAsync(x => x.Id == mapping.DocumentId && x.WikiId == wiki.Id && x.IsDeleted == 0, cancellationToken)
            : null;

        // 存储侧按 ObjectKey 幂等复用已上传文件，内容变化需先清理旧对象，避免拿到旧内容
        if (document != null && document.FileId > 0)
        {
            await _storageService.DeleteFilesAsync(new[] { (long)document.FileId }, cancellationToken);
        }

        using var stream = new MemoryStream(bytes);
        var upload = await _storageService.UploadStreamAsync(new UploadStreamFileCommand
        {
            FileStream = stream,
            ContentType = "text/markdown",
            FileSize = bytes.Length,
            SHA256 = item.ContentHash,
            ObjectKey = objectKey,
        }, cancellationToken);

        if (document == null)
        {
            document = new WikiDocumentEntity
            {
                WikiId = wiki.Id,
                FileId = (int)upload.FileId,
                FileName = fileName,
                ObjectKey = upload.ObjectKey,
                FileType = ".md",
                SliceConfig = string.Empty,
                VersionNo = 1,
                IsUpdate = true,
            };
            await _databaseContext.WikiDocuments.AddAsync(document, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            document.FileId = (int)upload.FileId;
            document.FileName = fileName;
            document.ObjectKey = upload.ObjectKey;
            document.IsUpdate = true;
            document.VersionNo += 1;
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        var contentEntity = await _databaseContext.WikiDocumentContents
            .FirstOrDefaultAsync(x => x.DocumentId == document.Id && x.WikiId == wiki.Id, cancellationToken);

        if (contentEntity == null)
        {
            contentEntity = new WikiDocumentContentEntity
            {
                Id = _idProvider.NextId(),
                WikiId = wiki.Id,
                DocumentId = document.Id,
                Content = item.Content,
            };
            await _databaseContext.WikiDocumentContents.AddAsync(contentEntity, cancellationToken);
        }
        else
        {
            contentEntity.Content = item.Content;
        }

        var workflowConfig = WikiWorkflowConfigJson.Deserialize(source.WorkflowConfig);
        var (success, message, taskId) = await _workflowRunner.RunAsync(wiki, document.Id, workflowConfig, cancellationToken);

        if (mapping == null)
        {
            mapping = new WikiSourceDocumentEntity
            {
                SourceId = source.Id,
                WikiId = wiki.Id,
                TeamId = source.TeamId,
                DocumentId = document.Id,
                ExternalKey = Truncate(item.ExternalKey, 500),
                ExternalDocToken = Truncate(item.ExternalDocToken, 500),
                ExternalTitle = Truncate(item.Title, 255),
                ExternalPath = Truncate(item.Path, 1000),
                ContentHash = item.ContentHash,
                Revision = Truncate(item.Revision, 64),
                Status = success ? (int)WikiSourceDocumentStatus.Synced : (int)WikiSourceDocumentStatus.Failed,
                LastSyncTime = DateTimeOffset.UtcNow,
                LastError = success ? string.Empty : Truncate(message, 1000),
            };
            await _databaseContext.WikiSourceDocuments.AddAsync(mapping, cancellationToken);
        }
        else
        {
            mapping.DocumentId = document.Id;
            mapping.ExternalDocToken = Truncate(item.ExternalDocToken, 500);
            mapping.ExternalTitle = Truncate(item.Title, 255);
            mapping.ExternalPath = Truncate(item.Path, 1000);
            mapping.ContentHash = item.ContentHash;
            mapping.Revision = Truncate(item.Revision, 64);
            mapping.Status = success ? (int)WikiSourceDocumentStatus.Synced : (int)WikiSourceDocumentStatus.Failed;
            mapping.LastSyncTime = DateTimeOffset.UtcNow;
            mapping.LastError = success ? string.Empty : Truncate(message, 1000);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return (document.Id, taskId, message);
    }

    /// <summary>
    /// 计算内容的 SHA-256 十六进制摘要，用于跨轮次增量比对.
    /// </summary>
    /// <param name="content">内容.</param>
    /// <returns>小写十六进制摘要.</returns>
    public static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// 把外部标识转为安全的存储对象 key 片段.
    /// </summary>
    /// <param name="key">外部标识.</param>
    /// <returns>安全片段.</returns>
    public static string SafeObjectKey(string key)
    {
        var builder = new StringBuilder(key.Length);
        foreach (var c in key)
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        }

        var result = builder.ToString().Trim('_');
        if (string.IsNullOrEmpty(result))
        {
            result = "index";
        }

        return result.Length > 180 ? result[..180] : result;
    }

    /// <summary>
    /// 把标题转为安全的文件名.
    /// </summary>
    /// <param name="title">标题.</param>
    /// <returns>安全文件名.</returns>
    public static string SafeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder();
        foreach (var c in title)
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        var result = builder.ToString().Trim();
        if (string.IsNullOrEmpty(result))
        {
            result = "未命名文档";
        }

        return result.Length > 100 ? result[..100] : result;
    }

    /// <summary>
    /// 截断字符串到指定长度.
    /// </summary>
    /// <param name="value">原值.</param>
    /// <param name="maxLength">最大长度.</param>
    /// <returns>截断后的值.</returns>
    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
