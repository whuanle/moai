using System.Text;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Models;
using MoAI.Storage.Services;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Base64 解码插件的行为测试：形态判定、参数校验、截断与转存路径（不访问真实存储）.
/// </summary>
public class Base64DecodePluginTests
{
    private static readonly byte[] PngBytes = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    };

    private readonly StubStorageService _storage = new();

    private Base64DecodePlugin CreatePlugin(int? maxDecodeBytes = null, int? textMaxLength = null)
    {
        var plugin = new Base64DecodePlugin(_storage);
        var error = plugin.InitAsync(new Base64DecodeConfig
        {
            MaxDecodeBytes = maxDecodeBytes ?? 2 * 1024 * 1024,
            TextMaxLength = textMaxLength ?? 20000,
        }).GetAwaiter().GetResult();
        Assert.Null(error);
        return plugin;
    }

    [Fact]
    public async Task InitAsync_ClampsFields()
    {
        var plugin = new Base64DecodePlugin(_storage);
        var error = await plugin.InitAsync(new Base64DecodeConfig
        {
            MaxDecodeBytes = 0,
            TextMaxLength = 999999999,
        });

        Assert.Null(error);

        // 钳制后的上下限通过运行行为间接验证：MaxDecodeBytes 钳到 1024，2000 字节输入被拒.
        var input = Convert.ToBase64String(new byte[2000]);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => plugin.RunAsync(new Base64DecodeRequest { Base64 = input }, CancellationToken.None));
        Assert.Contains("1024", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_BlankInput_ReturnsError()
    {
        var plugin = CreatePlugin();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => plugin.RunAsync(new Base64DecodeRequest { Base64 = "  " }, CancellationToken.None));

        Assert.Contains("Base64 不能为空", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("abc")] // 长度非 4 的倍数
    public async Task RunAsync_InvalidBase64_ReturnsError(string input)
    {
        var plugin = CreatePlugin();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => plugin.RunAsync(new Base64DecodeRequest { Base64 = input }, CancellationToken.None));

        Assert.Contains("不是有效的 Base64", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task RunAsync_PlainText_ReturnsText()
    {
        var plugin = CreatePlugin();

        var result = await plugin.RunAsync(new Base64DecodeRequest
        {
            Base64 = "SGVsbG8sIE1vQUkh",
        }, CancellationToken.None);

        Assert.Equal("text", result.Kind);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal("Hello, MoAI!", result.Text);
        Assert.False(result.TextTruncated);
        Assert.Equal(12, result.SizeBytes);
        Assert.Null(result.Url);
        Assert.Null(result.Markdown);
        Assert.Equal("decoded.txt", result.FileName);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task RunAsync_WhitespaceInBase64_StillDecodes()
    {
        var plugin = CreatePlugin();
        var base64 = Convert.ToBase64String("你好，MoAI"u8);
        var broken = string.Join("\r\n ", Enumerable.Range(0, base64.Length / 4).Select(i => base64.Substring(i * 4, 4)));

        var result = await plugin.RunAsync(new Base64DecodeRequest { Base64 = broken }, CancellationToken.None);

        Assert.Equal("text", result.Kind);
        Assert.Equal("你好，MoAI", result.Text);
    }

    [Fact]
    public async Task RunAsync_LongText_Truncated()
    {
        var plugin = CreatePlugin(textMaxLength: 100);
        var longText = new string('a', 250);

        var result = await plugin.RunAsync(new Base64DecodeRequest
        {
            Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(longText)),
        }, CancellationToken.None);

        Assert.Equal("text", result.Kind);
        Assert.True(result.TextTruncated);
        Assert.Equal(100, result.Text!.Length);
        Assert.Equal(250, result.SizeBytes);
    }

    [Fact]
    public async Task RunAsync_DataUrlPng_UploadsImage()
    {
        var plugin = CreatePlugin();
        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(PngBytes)}";

        var result = await plugin.RunAsync(new Base64DecodeRequest { Base64 = dataUrl }, CancellationToken.None);

        Assert.Equal("image", result.Kind);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(PngBytes.Length, result.SizeBytes);
        Assert.Null(result.Text);
        Assert.StartsWith("http://test.local/static/public/images/", result.Url, StringComparison.Ordinal);
        Assert.EndsWith(".png", result.Url, StringComparison.Ordinal);
        Assert.Equal($"![image]({result.Url})", result.Markdown);
        var upload = Assert.Single(_storage.Uploads);
        Assert.Equal(PngBytes.Length, upload.FileSize);
        Assert.Equal("image/png", upload.ContentType);
        Assert.StartsWith("public/images/", upload.ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RawPngBytes_UploadsImage()
    {
        var plugin = CreatePlugin();

        var result = await plugin.RunAsync(new Base64DecodeRequest
        {
            Base64 = Convert.ToBase64String(PngBytes),
            FileName = "shot.png",
        }, CancellationToken.None);

        Assert.Equal("image", result.Kind);
        Assert.Equal("shot.png", result.FileName);
        Assert.StartsWith("http://test.local/static/public/images/", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_JpegMagic_UploadsJpg()
    {
        var plugin = CreatePlugin();
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        var result = await plugin.RunAsync(new Base64DecodeRequest { Base64 = Convert.ToBase64String(jpeg) }, CancellationToken.None);

        Assert.Equal("image", result.Kind);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.EndsWith(".jpg", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SvgText_UploadsAsImage()
    {
        var plugin = CreatePlugin();
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>";

        var result = await plugin.RunAsync(new Base64DecodeRequest
        {
            Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)),
        }, CancellationToken.None);

        Assert.Equal("image", result.Kind);
        Assert.Equal("image/svg+xml", result.ContentType);
        Assert.Null(result.Text);
        Assert.EndsWith(".svg", result.Url, StringComparison.Ordinal);
        Assert.StartsWith("public/images/", _storage.Uploads[0].ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_BinaryWithoutMagic_UploadsAsFile()
    {
        var plugin = CreatePlugin();
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0x00, 0x03 };

        var result = await plugin.RunAsync(new Base64DecodeRequest { Base64 = Convert.ToBase64String(bytes) }, CancellationToken.None);

        Assert.Equal("binary", result.Kind);
        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.Equal(bytes.Length, result.SizeBytes);
        Assert.Equal($"[file]({result.Url})", result.Markdown);
        Assert.EndsWith(".bin", result.Url, StringComparison.Ordinal);
        Assert.StartsWith("public/files/", _storage.Uploads[0].ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DataUrlPdf_UsesDeclaredType()
    {
        var plugin = CreatePlugin();
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x00, 0x0A };
        var dataUrl = $"data:application/pdf;base64,{Convert.ToBase64String(bytes)}";

        var result = await plugin.RunAsync(new Base64DecodeRequest { Base64 = dataUrl, FileName = "report.pdf" }, CancellationToken.None);

        Assert.Equal("binary", result.Kind);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("report.pdf", result.FileName);
        Assert.EndsWith(".pdf", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ExceedsMaxDecodeBytes_ReturnsError()
    {
        var plugin = CreatePlugin(maxDecodeBytes: 1024);
        var input = Convert.ToBase64String(new byte[2048]);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => plugin.RunAsync(new Base64DecodeRequest { Base64 = input }, CancellationToken.None));

        Assert.Contains("超过上限 1024 字节", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task RunAsync_OversizedInput_RejectedBeforeDecode()
    {
        // 5000 个 A 解码后实际合法且约 3750 字节，粗估阶段就应拒绝，不解码不落存储.
        var plugin = CreatePlugin(maxDecodeBytes: 1024);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => plugin.RunAsync(new Base64DecodeRequest { Base64 = new string('A', 5000) }, CancellationToken.None));

        Assert.Contains("超过上限", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task RunAsync_SameContentTwice_IdempotentObjectKey()
    {
        var plugin = CreatePlugin();
        var input = Convert.ToBase64String(PngBytes);

        var first = await plugin.RunAsync(new Base64DecodeRequest { Base64 = input }, CancellationToken.None);
        var second = await plugin.RunAsync(new Base64DecodeRequest { Base64 = input }, CancellationToken.None);

        Assert.Equal(first.Url, second.Url);
    }

    [Fact]
    public async Task RunAsync_FileNameWithoutExtension_EchoesAsGiven()
    {
        var plugin = CreatePlugin();

        var result = await plugin.RunAsync(new Base64DecodeRequest
        {
            Base64 = Convert.ToBase64String(PngBytes),
            FileName = "shot",
        }, CancellationToken.None);

        Assert.Equal("shot", result.FileName);
        Assert.EndsWith(".png", result.Url, StringComparison.Ordinal);
    }
}

/// <summary>
/// 测试用存储桩：只实现上传与公开地址拼接，其余成员不可达.
/// </summary>
internal sealed class StubStorageService : IStorageService
{
    public List<UploadStreamFileCommand> Uploads { get; } = new();

    public string Server { get; set; } = "http://test.local";

    public Task<PreUploadFileCommandResponse> PreUploadAsync(PreUploadFileCommand command, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<FileUploadResult> UploadStreamAsync(UploadStreamFileCommand command, CancellationToken cancellationToken = default)
    {
        Uploads.Add(command);
        return Task.FromResult(new FileUploadResult
        {
            FileId = 1,
            ObjectKey = command.ObjectKey,
            FileSha256 = command.SHA256,
            FileType = command.ContentType,
        });
    }

    public Task<string> CompleteAsync(long fileId, bool isSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteFilesAsync(IReadOnlyCollection<long> fileIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<string, Uri>> GetDownloadUrlsAsync(IReadOnlyCollection<KeyValueString> objectKeys, TimeSpan expiryDuration, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<Uri> GetDownloadUrlAsync(string objectKey, string fileName, TimeSpan expiryDuration, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<bool> FileExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<StorageFileReadResult> ReadAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Uri GetPublicFileUrl(string objectKey)
    {
        var key = objectKey.Trim('/');
        var escaped = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"{Server}/static/{escaped}");
    }

    public Task<StorageFileInfo?> GetFileInfoAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
