using Maomi.ToMarkdown;
using MediatR;
using Microsoft.Extensions.Logging;
using MoAI.App.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Models;
using MoAI.Storage.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// 对话附件文本提取命令处理器：从存储读取公开 chat 目录的文件，
/// 用 Maomi.ToMarkdown 提取 markdown 并按上限截断（防止超长文档撑爆模型上下文）.
/// </summary>
public class ExtractChatAttachmentCommandHandler : IRequestHandler<ExtractChatAttachmentCommand, ExtractChatAttachmentResponse>
{
    /// <summary>
    /// 提取文本截断上限（约 3 万 token 量级的字符预算）.
    /// </summary>
    public const int MaxContentLength = 120_000;

    private readonly IStorageService _storageService;
    private readonly TextExtractionService _textExtractionService;
    private readonly ILogger<ExtractChatAttachmentCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractChatAttachmentCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="textExtractionService">文本抽取服务.</param>
    /// <param name="logger">日志.</param>
    public ExtractChatAttachmentCommandHandler(
        IStorageService storageService,
        TextExtractionService textExtractionService,
        ILogger<ExtractChatAttachmentCommandHandler> logger)
    {
        _storageService = storageService;
        _textExtractionService = textExtractionService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExtractChatAttachmentResponse> Handle(ExtractChatAttachmentCommand request, CancellationToken cancellationToken)
    {
        StorageFileReadResult file;
        try
        {
            file = await _storageService.ReadAsync(request.ObjectKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "对话附件读取失败 objectKey={ObjectKey}", request.ObjectKey);
            throw new BusinessException("附件不存在或已被清理.") { StatusCode = 404 };
        }

        string markdown;
        await using (file.FileStream)
        {
            try
            {
                markdown = await _textExtractionService.ExtractAsync(file.FileStream, request.FileName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "对话附件提取失败 objectKey={ObjectKey} fileName={FileName}", request.ObjectKey, request.FileName);
                throw new BusinessException("附件内容提取失败，请确认文件格式是否受支持.") { StatusCode = 400 };
            }
        }

        var length = markdown.Length;
        var truncated = false;
        if (length > MaxContentLength)
        {
            markdown = markdown[..MaxContentLength];
            truncated = true;
        }

        return new ExtractChatAttachmentResponse
        {
            Markdown = markdown,
            ContentLength = length,
            Truncated = truncated,
        };
    }
}
