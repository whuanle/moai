using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.AI.Services;
using Moq;
using MoAI.Storage.Models;
using MoAI.Storage.Services;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 对话附件图片标记 → 多模态 DataContent 的重写逻辑.
/// </summary>
public class ChatAttachmentImageRewriterTests
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47];

    private static StorageFileReadResult FileResult(byte[] data, string contentType = "image/png")
        => new()
        {
            FileStream = new MemoryStream(data),
            ContentType = contentType,
            FileSize = data.Length,
            FileExtension = ".png",
        };

    private static Mock<IStorageService> StorageWith(params (string Key, StorageFileReadResult Result)[] entries)
    {
        var storage = new Mock<IStorageService>();
        foreach (var (key, result) in entries)
        {
            storage.Setup(x => x.ReadAsync(key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        return storage;
    }

    private static Task<List<ChatMessage>> RewriteAsync(
        IEnumerable<ChatMessage> messages,
        IStorageService storage,
        Dictionary<string, (string MediaType, byte[] Data)>? cache = null)
        => ChatAttachmentImageRewriter.RewriteAsync(
            messages,
            storage,
            cache ?? [],
            NullLogger.Instance,
            CancellationToken.None);

    [Fact]
    public async Task Rewrite_ImageBlockWithObjectKey_ReplacesWithPlaceholderAndDataContent()
    {
        var storage = StorageWith(("public/chat/xyz.png", FileResult(PngBytes)));
        var message = new ChatMessage(ChatRole.User, """
            看看这张图

            <moai-attachment name="截图.png" objectKey="public/chat/xyz.png">
            http://127.0.0.1:5000/static/public/chat/xyz.png
            </moai-attachment>
            """);

        var result = await RewriteAsync([message], storage.Object);

        var rewritten = Assert.Single(result);
        Assert.NotSame(message, rewritten);
        Assert.Equal("看看这张图\n\n[图片附件：截图.png]", rewritten.Text);
        var image = Assert.IsType<DataContent>(rewritten.Contents.OfType<DataContent>().Single());
        Assert.Equal("image/png", image.MediaType);
        Assert.True(image.Data.Length > 0);
    }

    [Fact]
    public async Task Rewrite_LegacyImageBlock_ExtractsObjectKeyFromStaticUrl()
    {
        var storage = StorageWith(("public/chat/xyz.png", FileResult(PngBytes, contentType: string.Empty)));
        var message = new ChatMessage(ChatRole.User, """
            <moai-attachment name="截图.png">
            [图片附件](http://127.0.0.1:5000/static/public/chat/xyz.png)
            </moai-attachment>
            """);

        var result = await RewriteAsync([message], storage.Object);

        var rewritten = Assert.Single(result);
        // 存储未返回 ContentType 时按扩展名推导
        var image = Assert.IsType<DataContent>(rewritten.Contents.OfType<DataContent>().Single());
        Assert.Equal("image/png", image.MediaType);
    }

    [Fact]
    public async Task Rewrite_DocumentBlockAndAssistantMessage_Untouched()
    {
        var storage = new Mock<IStorageService>();
        var user = new ChatMessage(ChatRole.User, """
            总结

            <moai-attachment name="报告.docx">
            # 季度报告
            </moai-attachment>
            """);
        var assistant = new ChatMessage(ChatRole.Assistant, "好的 <moai-attachment name=\"x.png\">\n[图片附件](http://h/static/public/chat/a.png)\n</moai-attachment>");

        var result = await RewriteAsync([user, assistant], storage.Object);

        Assert.Same(user, result[0]);
        Assert.Same(assistant, result[1]);
        storage.Verify(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rewrite_ObjectKeyOutsideChatDirectory_KeepsTextWithoutFetch()
    {
        var storage = new Mock<IStorageService>();
        var message = new ChatMessage(ChatRole.User, """
            <moai-attachment name="a.png" objectKey="private/secret/a.png">
            http://127.0.0.1:5000/static/private/secret/a.png
            </moai-attachment>
            """);

        var result = await RewriteAsync([message], storage.Object);

        Assert.Same(message, result[0]);
        storage.Verify(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rewrite_StorageReadFails_DegradesToOriginalText()
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.ReadAsync("public/chat/missing.png", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("not found"));
        var message = new ChatMessage(ChatRole.User, """
            <moai-attachment name="a.png" objectKey="public/chat/missing.png">
            http://127.0.0.1:5000/static/public/chat/missing.png
            </moai-attachment>
            """);

        var result = await RewriteAsync([message], storage.Object);

        Assert.Same(message, result[0]);
        Assert.Empty(result[0].Contents.OfType<DataContent>());
    }

    [Fact]
    public async Task Rewrite_SvgImage_DoesNotInline()
    {
        var storage = new Mock<IStorageService>();
        var message = new ChatMessage(ChatRole.User, """
            <moai-attachment name="icon.svg" objectKey="public/chat/icon.svg">
            http://127.0.0.1:5000/static/public/chat/icon.svg
            </moai-attachment>
            """);

        var result = await RewriteAsync([message], storage.Object);

        // 主流视觉接口不接受 image/svg+xml，svg 保持链接文本
        Assert.Same(message, result[0]);
        storage.Verify(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rewrite_MultipleImages_AppendsInOrderAndCachesBytes()
    {
        var jpg = new byte[] { 0xFF, 0xD8 };
        var storage = StorageWith(
            ("public/chat/a.jpg", FileResult(jpg, contentType: string.Empty)),
            ("public/chat/b.png", FileResult(PngBytes)));
        var message = new ChatMessage(ChatRole.User, """
            <moai-attachment name="a.jpg" objectKey="public/chat/a.jpg">
            http://h/static/public/chat/a.jpg
            </moai-attachment>

            <moai-attachment name="b.png" objectKey="public/chat/b.png">
            http://h/static/public/chat/b.png
            </moai-attachment>
            """);

        var result = await RewriteAsync([message], storage.Object);

        var images = result[0].Contents.OfType<DataContent>().ToList();
        Assert.Equal(2, images.Count);
        Assert.Equal("image/jpeg", images[0].MediaType);
        Assert.Equal("image/png", images[1].MediaType);
        Assert.Equal("[图片附件：a.jpg]\n\n[图片附件：b.png]", result[0].Text);
    }

    [Fact]
    public async Task Rewrite_SharedCache_SkipsRepeatStorageRead()
    {
        var storage = StorageWith(("public/chat/x.png", FileResult(PngBytes)));
        var message = new ChatMessage(ChatRole.User, "<moai-attachment name=\"x.png\" objectKey=\"public/chat/x.png\">\nhttp://h/static/public/chat/x.png\n</moai-attachment>");
        var cache = new Dictionary<string, (string MediaType, byte[] Data)>();

        await RewriteAsync([message], storage.Object, cache);
        await RewriteAsync([message], storage.Object, cache);

        storage.Verify(x => x.ReadAsync("public/chat/x.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MayContainAttachment_FastPath()
    {
        Assert.True(ChatAttachmentImageRewriter.MayContainAttachment("a <moai-attachment name=\"x\">"));
        Assert.False(ChatAttachmentImageRewriter.MayContainAttachment("普通文本"));
        Assert.False(ChatAttachmentImageRewriter.MayContainAttachment(null));
    }
}
