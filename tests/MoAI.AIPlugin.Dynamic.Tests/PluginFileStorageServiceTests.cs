using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Put;
using MoAI.Storage.Commands;
using MoAI.Storage.Models;
using MoAI.Storage.Services;
using Moq;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 插件文件存储服务测试：内容寻址、公开/私有地址与公网文件转存（不访问真实存储与网络）.
/// </summary>
public class PluginFileStorageServiceTests
{
    private const long FileId = 42;

    [Fact]
    public async Task SaveAsync_SeekableStream_UploadsContentAddressedFile()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };
        var storage = CreateStorage();
        UploadStreamFileCommand? uploaded = null;
        storage
            .Setup(x => x.UploadStreamAsync(It.IsAny<UploadStreamFileCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UploadStreamFileCommand, CancellationToken>((command, _) => uploaded = command)
            .ReturnsAsync(new FileUploadResult { FileId = FileId, ObjectKey = "public/images/x.png" });

        var service = new PluginFileStorageService(storage.Object, Mock.Of<IPutClient>());
        using var stream = new MemoryStream(content);

        var id = await service.SaveAsync(stream, new PluginFileSaveOptions
        {
            FileName = "chart.png",
            IsPublic = true,
            Prefix = "images",
        });

        Assert.Equal(FileId, id);
        Assert.NotNull(uploaded);
        Assert.Equal("image/png", uploaded!.ContentType);
        Assert.Equal(content.Length, uploaded.FileSize);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), uploaded.SHA256);
        Assert.StartsWith("public/images/", uploaded.ObjectKey, StringComparison.Ordinal);
        Assert.EndsWith(".png", uploaded.ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_NonSeekableStream_UploadsAndComputesSha()
    {
        var content = new byte[] { 9, 8, 7 };
        var storage = CreateStorage();
        UploadStreamFileCommand? uploaded = null;
        storage
            .Setup(x => x.UploadStreamAsync(It.IsAny<UploadStreamFileCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UploadStreamFileCommand, CancellationToken>((command, _) => uploaded = command)
            .ReturnsAsync(new FileUploadResult { FileId = FileId, ObjectKey = "plugins/x.bin" });

        var service = new PluginFileStorageService(storage.Object, Mock.Of<IPutClient>());
        await using var stream = new NonSeekableStream(content);

        var id = await service.SaveAsync(stream, new PluginFileSaveOptions { FileName = "data.bin" });

        Assert.Equal(FileId, id);
        Assert.NotNull(uploaded);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), uploaded!.SHA256);
        Assert.StartsWith("plugins/", uploaded.ObjectKey, StringComparison.Ordinal);
        Assert.Equal(content.Length, uploaded.FileSize);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_ById_UsesStoredObjectKey()
    {
        var storage = CreateStorage();
        storage
            .Setup(x => x.GetFileInfoByIdAsync(FileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageFileInfo
            {
                FileId = FileId,
                ObjectKey = "plugins/report.pdf",
                FileExtension = ".pdf",
                ContentType = "application/pdf",
                FileSize = 10,
                FileSha256 = "abc",
            });

        string? requestedFileName = null;
        storage
            .Setup(x => x.GetDownloadUrlAsync("plugins/report.pdf", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, TimeSpan, CancellationToken>((_, fileName, _, _) => requestedFileName = fileName)
            .ReturnsAsync(new Uri("https://s3.local/report.pdf"));

        var service = new PluginFileStorageService(storage.Object, Mock.Of<IPutClient>());
        var url = await service.GetDownloadUrlAsync(FileId);

        Assert.Equal("https://s3.local/report.pdf", url.ToString());
        Assert.Equal("file-42.pdf", requestedFileName);
    }

    [Fact]
    public async Task GetPublicUrlAsync_NonPublicFile_ReturnsNull()
    {
        var storage = CreateStorage();
        storage
            .Setup(x => x.GetFileInfoByIdAsync(FileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageFileInfo
            {
                FileId = FileId,
                ObjectKey = "plugins/report.pdf",
                FileExtension = ".pdf",
                ContentType = "application/pdf",
                FileSize = 10,
                FileSha256 = "abc",
            });

        var service = new PluginFileStorageService(storage.Object, Mock.Of<IPutClient>());

        Assert.Null(await service.GetPublicUrlAsync(FileId));
        storage.Verify(x => x.GetPublicFileUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SaveFromUrlAsync_DownloadsAndStoresWithResponseMetadata()
    {
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var storage = CreateStorage();
        UploadStreamFileCommand? uploaded = null;
        storage
            .Setup(x => x.UploadStreamAsync(It.IsAny<UploadStreamFileCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UploadStreamFileCommand, CancellationToken>((command, _) => uploaded = command)
            .ReturnsAsync(new FileUploadResult { FileId = FileId, ObjectKey = "public/images/x.jpg" });

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "remote.jpg",
            };
            return response;
        }));

        var putClient = new Mock<IPutClient>();
        putClient.SetupGet(x => x.Client).Returns(httpClient);

        var service = new PluginFileStorageService(storage.Object, putClient.Object);
        var id = await service.SaveFromUrlAsync("https://example.com/download?id=1", new PluginFileSaveOptions
        {
            IsPublic = true,
            Prefix = "images",
        });

        Assert.Equal(FileId, id);
        Assert.NotNull(uploaded);
        Assert.Equal("image/jpeg", uploaded!.ContentType);
        Assert.Equal(content.Length, uploaded.FileSize);
        Assert.EndsWith(".jpg", uploaded.ObjectKey, StringComparison.Ordinal);
    }

    private static Mock<IStorageService> CreateStorage()
    {
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        storage
            .Setup(x => x.UploadStreamAsync(It.IsAny<UploadStreamFileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult { FileId = FileId });
        return storage;
    }

    /// <summary>不可定位流包装，模拟网络流.</summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] content)
        {
            _inner = new MemoryStream(content);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
