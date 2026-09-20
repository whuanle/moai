using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.Storage.Models;
using MoAI.Storage.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 对话附件图片标记重写器：把用户消息中的图片附件标记块转换为多模态 <see cref="DataContent"/>（image/* 字节内联），
/// 文档标记块保持原样（提取文本已由前端内联）.
/// <para>
/// 会话落库始终存标记文本（见 <c>ChatMessageMapper</c>），本重写仅作用于发给模型的请求，
/// 因此历史回放与新消息走同一转换路径；图片读取失败时降级保留原标记文本（模型退化为链接提示）.
/// </para>
/// </summary>
public static class ChatAttachmentImageRewriter
{
    /// <summary>图片内联大小上限（与对话附件上传上限一致）.</summary>
    public const long MaxImageSize = 20 * 1024 * 1024;

    private const string AttachmentTag = "moai-attachment";

    // 与前端 IMAGE_EXTENSIONS 对齐；svg 不做多模态注入（主流视觉接口不接受 image/svg+xml），保持链接文本
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
    };

    private static readonly Dictionary<string, string> ExtensionMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
    };

    private static readonly Regex AttachmentRegex = new(
        @"<moai-attachment name=""(?<name>[^""]*)""(?: objectKey=""(?<key>[^""]*)"")?>\r?\n?(?<body>[\s\S]*?)\r?\n?</moai-attachment>",
        RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(@"https?://[^\s)""'<>]+", RegexOptions.Compiled);

    /// <summary>
    /// 判断文本是否可能含附件标记（无标记时跳过整个重写，避免逐条正则）.
    /// </summary>
    /// <param name="text">消息文本.</param>
    /// <returns>是否含标记.</returns>
    public static bool MayContainAttachment(string? text)
        => text?.Contains('<' + AttachmentTag, StringComparison.Ordinal) == true;

    /// <summary>
    /// 重写消息列表：用户消息中的图片附件标记块 → 文本占位 + 追加 <see cref="ImageContent"/>；其余消息原样返回.
    /// </summary>
    /// <param name="messages">待发送消息.</param>
    /// <param name="storageService">存储服务（读取 public/chat 图片字节）.</param>
    /// <param name="cache">图片字节缓存（key=objectKey，避免同轮工具循环重复读取）.</param>
    /// <param name="logger">日志.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>重写后的消息列表（无图片时复用原消息实例）.</returns>
    public static async Task<List<ChatMessage>> RewriteAsync(
        IEnumerable<ChatMessage> messages,
        IStorageService storageService,
        Dictionary<string, (string MediaType, byte[] Data)> cache,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = new List<ChatMessage>();
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User || !MayContainAttachment(message.Text))
            {
                result.Add(message);
                continue;
            }

            result.Add(await RewriteUserMessageAsync(message, storageService, cache, logger, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    private static async Task<ChatMessage> RewriteUserMessageAsync(
        ChatMessage message,
        IStorageService storageService,
        Dictionary<string, (string MediaType, byte[] Data)> cache,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var text = message.Text ?? string.Empty;
        var images = new List<DataContent>();
        var builder = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in AttachmentRegex.Matches(text))
        {
            var name = match.Groups["name"].Value;
            var body = match.Groups["body"].Value;
            if (!IsImageAttachment(name, body))
            {
                continue;
            }

            var objectKey = ResolveObjectKey(match.Groups["key"].Value, body);
            var image = objectKey == null ? null : await TryLoadImageAsync(objectKey, name, storageService, cache, logger, cancellationToken).ConfigureAwait(false);
            if (image == null)
            {
                continue;
            }

            builder.Append(text, lastIndex, match.Index - lastIndex);
            builder.Append($"[图片附件：{name}]");
            images.Add(image);
            lastIndex = match.Index + match.Length;
        }

        if (images.Count == 0)
        {
            return message;
        }

        builder.Append(text, lastIndex, text.Length - lastIndex);

        var contents = new List<AIContent> { new TextContent(builder.ToString()) };
        contents.AddRange(message.Contents.Where(x => x is not TextContent));
        contents.AddRange(images);
        return new ChatMessage(message.Role, contents) { AuthorName = message.AuthorName, RawRepresentation = message.RawRepresentation };
    }

    private static bool IsImageAttachment(string name, string body)
    {
        var extension = GetExtension(name);
        if (ImageExtensions.Contains(extension))
        {
            return true;
        }

        // 历史消息兼容：图片块内容为 [图片附件](url)
        return body.AsSpan().TrimStart().StartsWith("[图片附件](", StringComparison.Ordinal);
    }

    private static string? ResolveObjectKey(string attributeKey, string body)
    {
        var key = attributeKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            var url = UrlRegex.Match(body);
            if (!url.Success)
            {
                return null;
            }

            var marker = url.Value.IndexOf("/static/", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                return null;
            }

            key = Uri.UnescapeDataString(url.Value[(marker + "/static/".Length)..]);
        }

        // 与 ExtractChatAttachmentCommand 相同的越权约束：仅允许公开对话附件目录
        if (!key.StartsWith("public/chat/", StringComparison.OrdinalIgnoreCase) || key.Contains(".."))
        {
            return null;
        }

        return key.TrimEnd('/');
    }

    private static async Task<DataContent?> TryLoadImageAsync(
        string objectKey,
        string fileName,
        IStorageService storageService,
        Dictionary<string, (string MediaType, byte[] Data)> cache,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(objectKey, out var cached))
        {
            return new DataContent(cached.Data, cached.MediaType);
        }

        try
        {
            StorageFileReadResult file;
            try
            {
                file = await storageService.ReadAsync(objectKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "对话图片附件读取失败 objectKey={ObjectKey}", objectKey);
                return null;
            }

            await using (file.FileStream)
            {
                if (file.FileSize > MaxImageSize)
                {
                    logger.LogWarning("对话图片附件超过内联上限 objectKey={ObjectKey} size={Size}", objectKey, file.FileSize);
                    return null;
                }

                using var buffer = new MemoryStream();
                await file.FileStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                var mediaType = !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : MediaTypeOf(fileName);
                if (mediaType == null)
                {
                    return null;
                }

                var data = buffer.ToArray();
                cache[objectKey] = (mediaType, data);
                return new DataContent(data, mediaType);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "对话图片附件内联失败 objectKey={ObjectKey}", objectKey);
            return null;
        }
    }

    private static string? MediaTypeOf(string fileName)
        => ExtensionMediaTypes.TryGetValue(GetExtension(fileName), out var mediaType) ? mediaType : null;

    private static string GetExtension(string fileName)
    {
        var index = fileName.LastIndexOf('.');
        return index < 0 ? string.Empty : fileName[index..];
    }
}
