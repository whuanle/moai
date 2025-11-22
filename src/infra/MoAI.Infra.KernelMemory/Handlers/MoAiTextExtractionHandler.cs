#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Maomi;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.Text;
using MoAI.Infra.Put;
using System.IO;
using System.Text;

namespace MoAI.Infra.Handlers;

/// <summary>
/// Memory ingestion pipeline handler responsible for extracting text from files and saving it to document storage.
/// </summary>
[InjectOnTransient]
public sealed class MoAiTextExtractionHandler : IDisposable
{
    private readonly IWebScraper _webScraper;
    private readonly ILogger<MoAiTextExtractionHandler> _log;
    private readonly MimeTypesDetection _mimeTypeDetection = new MimeTypesDetection();
    private readonly TextExtractionFactory _textExtractionFactory;
    private readonly IPutClient _putClient;

    /// <summary>
    /// TextExtraction.
    /// </summary>
    public TextExtractionFactory TextExtraction => _textExtractionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoAiTextExtractionHandler"/> class.
    /// </summary>
    /// <param name="textExtractionFactory"></param>
    /// <param name="putClient"></param>
    /// <param name="webScraper"></param>
    /// <param name="loggerFactory"></param>
    public MoAiTextExtractionHandler(
        TextExtractionFactory textExtractionFactory,
        IPutClient putClient,
        IWebScraper? webScraper = null,
        ILoggerFactory? loggerFactory = null)
    {
        _textExtractionFactory = textExtractionFactory;
        _log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<MoAiTextExtractionHandler>();
        _webScraper = webScraper ?? new WebScraper();
        _putClient = putClient;
    }

    /// <summary>
    /// Extract file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException">.</exception>
    public async Task<(string Text, FileContent FileContent)> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            this._log.LogError("File not found: {FilePath}", filePath);
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        this._log.LogTrace("File exists, reading {FilePath}", filePath);
        FileInfo fileInfo = new(filePath);
        var fileType = this._mimeTypeDetection.GetFileType(fileInfo.Name);
        using var stream = File.OpenRead(filePath);
        (var text, var content) = await Extract(fileName: fileInfo.Name, fileType: fileType, stream: stream, cancellationToken: cancellationToken);
        return (text, content);
    }

    /// <summary>
    /// Extract file.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="fileName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException">.</exception>
    public async Task<(string Text, FileContent FileContent)> ExtractUrlAsync(string url, string fileName, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult))
        {
            this._log.LogError("Uri format error: {Url}", url);
            throw new UriFormatException($"Uri format error: {{url}}: {url}");
        }

        var filePath = Path.GetTempFileName();
        using var stream = File.Create(filePath);
        _putClient.Client.BaseAddress = new Uri(url);
        using var httpStream = await _putClient.DownloadAsync(string.Empty);
        await httpStream.CopyToAsync(stream, cancellationToken);
        await stream.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);


        (var text, var content) = await Extract(fileName: filePath, fileType: this._mimeTypeDetection.GetFileType(fileName), stream: stream, cancellationToken: cancellationToken);
        return (text, content);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_webScraper is not IDisposable x)
        {
            return;
        }

        x.Dispose();
    }

    private async Task<(string Text, FileContent FileContent)> Extract(string fileName, string fileType, Stream stream, CancellationToken cancellationToken)
    {
        Task<Stream> AsyncStreamDelegate() => Task.FromResult<Stream>(stream);
        using StreamableFileContent streamableContent = new(fileName, stream.Length, fileType, DateTimeOffset.Now, AsyncStreamDelegate);

        var fileContent = await BinaryData.FromStreamAsync(await streamableContent.GetStreamAsync().ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

        string text = string.Empty;
        FileContent content = new(MimeTypes.PlainText);
        bool skipFile = false;

        if (fileContent.ToArray().Length > 0)
        {
            if (fileType == MimeTypes.WebPageUrl)
            {
                var (pageContent, skip) = await DownloadContentAsync(fileType, fileName, fileContent, cancellationToken).ConfigureAwait(false);
                skipFile = skip;
                if (!skipFile)
                {
                    (text, content, skipFile) = await ExtractTextAsync(fileType, fileName, pageContent, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                (text, content, skipFile) = await ExtractTextAsync(fileType, fileName, fileContent, cancellationToken).ConfigureAwait(false);
            }
        }

        return (text, content);
    }

    private async Task<(BinaryData PageContent, bool Skip)> DownloadContentAsync(string mineType, string fileName, BinaryData fileContent, CancellationToken cancellationToken)
    {
        var url = fileContent.ToString();
        _log.LogDebug("Downloading web page specified in '{FileName}' and extracting text from '{Url}'", fileName, url);
        if (string.IsNullOrWhiteSpace(url))
        {
            _log.LogWarning("The web page URL is empty");
            return (fileContent, Skip: true);
        }

        var urlDownloadResult = await _webScraper.GetContentAsync(url, cancellationToken).ConfigureAwait(false);
        if (!urlDownloadResult.Success)
        {
            _log.LogWarning("Web page download error: {Error}", urlDownloadResult.Error);
            return (fileContent, Skip: true);
        }

        if (urlDownloadResult.Content.Length == 0)
        {
            _log.LogWarning("The web page has no text content, skipping it");
            return (fileContent, Skip: true);
        }

        return (urlDownloadResult.Content, Skip: false);
    }

    private async Task<(string Text, FileContent Content, bool SkipFile)> ExtractTextAsync(string mineType, string fileName, BinaryData fileContent, CancellationToken cancellationToken)
    {
        // Define default empty content
        var content = new FileContent(MimeTypes.PlainText);

        if (string.IsNullOrEmpty(mineType))
        {
            _log.LogWarning("Empty MIME type, file '{FileName}' will be ignored", fileName);
            return (string.Empty, content, true);
        }

        // Checks if there is a decoder that supports the file MIME type. If multiple decoders support this type, it means that
        // the decoder has been redefined, so it takes the last one.
        var decoder = _textExtractionFactory.Decoders.LastOrDefault(d => d.SupportsMimeType(mineType));
        if (decoder is not null)
        {
            _log.LogDebug("Extracting text from file '{FileName}' mime type '{MineType}' using extractor '{Decoder}'", fileName, mineType, decoder.GetType().FullName);
            content = await decoder.DecodeAsync(fileContent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _log.LogWarning("File MIME type not supported: {MineType} - ignoring the file {FileName}", mineType, fileName);
            return (string.Empty, content, true);
        }

        var textBuilder = new StringBuilder();
        foreach (var section in content.Sections)
        {
            var sectionContent = section.Content.Trim();
            if (string.IsNullOrEmpty(sectionContent))
            {
                continue;
            }

            textBuilder.Append(sectionContent);

            // Add a clean page separation
            if (section.SentencesAreComplete)
            {
                textBuilder.AppendLineNix();
                textBuilder.AppendLineNix();
            }
        }

        var text = textBuilder.ToString().Trim();

        return (text, content, false);
    }
}
