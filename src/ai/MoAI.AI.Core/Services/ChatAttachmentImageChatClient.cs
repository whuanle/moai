using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.Storage.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 对话附件图片多模态注入装饰器：请求发给模型前，把用户消息中的图片附件标记块
/// 重写为 <see cref="DataContent"/>（image/* 字节内联，见 <see cref="ChatAttachmentImageRewriter"/>）.
/// <para>
/// 会话持久化发生在 agent 层（存标记文本），本装饰器只改发往模型的请求；
/// 同一实例（一次对话运行）内缓存图片字节，避免工具循环轮次重复读存储.
/// </para>
/// </summary>
public sealed class ChatAttachmentImageChatClient : DelegatingChatClient
{
    private readonly IStorageService _storageService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, (string MediaType, byte[] Data)> _imageCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatAttachmentImageChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">内层对话客户端.</param>
    /// <param name="storageService">存储服务.</param>
    /// <param name="logger">日志.</param>
    public ChatAttachmentImageChatClient(IChatClient innerClient, IStorageService storageService, ILogger<ChatAttachmentImageChatClient> logger)
        : base(innerClient)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var rewritten = await ChatAttachmentImageRewriter.RewriteAsync(messages, _storageService, _imageCache, _logger, cancellationToken).ConfigureAwait(false);
        return await base.GetResponseAsync(rewritten, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rewritten = await ChatAttachmentImageRewriter.RewriteAsync(messages, _storageService, _imageCache, _logger, cancellationToken).ConfigureAwait(false);
        await foreach (var update in base.GetStreamingResponseAsync(rewritten, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }
}
