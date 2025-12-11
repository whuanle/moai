#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Maomi;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.Text;
using MoAI.AI.TextExtract;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Put;
using System.IO;
using System.Text;

namespace MoAI.Infra.Handlers;

/// <summary>
/// KM 自带的文本提取器.
/// </summary>
[InjectOnTransient]
public sealed class KmTextExtractionHandler : IDisposable
{
    private readonly IWebScraper _webScraper;
    private readonly ILogger<KmTextExtractionHandler> _log;
    private readonly MimeTypesDetection _mimeTypeDetection = new MimeTypesDetection();
    private readonly TextExtractionFactory _textExtractionFactory;
    private readonly IPutClient _putClient;

    /// <summary>
    /// TextExtraction.
    /// </summary>
    public TextExtractionFactory TextExtraction => _textExtractionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="KmTextExtractionHandler"/> class.
    /// </summary>
    /// <param name="textExtractionFactory"></param>
    /// <param name="putClient"></param>
    /// <param name="webScraper"></param>
    /// <param name="loggerFactory"></param>
    public KmTextExtractionHandler(
        TextExtractionFactory textExtractionFactory,
        IPutClient putClient,
        IWebScraper? webScraper = null,
        ILoggerFactory? loggerFactory = null)
    {
        _textExtractionFactory = textExtractionFactory;
        _log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<KmTextExtractionHandler>();
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
    public async Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        this._log.LogTrace("File exists, reading {FilePath}", filePath);
        FileInfo fileInfo = new(filePath);
        var fileType = this._mimeTypeDetection.GetFileType(fileInfo.Name);
        using var stream = File.OpenRead(filePath);
        return await Extract(fileName: fileInfo.Name, fileType: fileType, stream: stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Extract file.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="fileName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException">.</exception>
    public async Task<string> ExtractUrlAsync(Uri url, string fileName, CancellationToken cancellationToken = default)
    {
        var filePath = Path.GetTempFileName();
        using var stream = File.Create(filePath);
        _putClient.Client.BaseAddress = url;
        using var httpStream = await _putClient.DownloadAsync(string.Empty);
        await httpStream.CopyToAsync(stream, cancellationToken);
        await stream.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        return await Extract(fileName: filePath, fileType: this._mimeTypeDetection.GetFileType(fileName), stream: stream, cancellationToken: cancellationToken);
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

    private async Task<string> Extract(string fileName, string fileType, Stream stream, CancellationToken cancellationToken)
    {
        return await ExtractTextAsync(fileType, fileName, stream, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ExtractTextAsync(string mineType, string fileName, Stream fileContent, CancellationToken cancellationToken)
    {
        // Define default empty content
        var content = new FileContent(MimeTypes.PlainText);

        if (string.IsNullOrEmpty(mineType))
        {
            throw new BusinessException("Empty MIME type, file '{0}' will be ignored", fileName);
        }

        // Checks if there is a decoder that supports the file MIME type. If multiple decoders support this type, it means that
        // the decoder has been redefined, so it takes the last one.
        var decoder = _textExtractionFactory.Decoders.LastOrDefault(d => d.SupportsMimeType(mineType));
        if (decoder is not null)
        {
            content = await decoder.DecodeAsync(fileContent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new BusinessException("File MIME type not supported: {0} - ignoring the file {1}", mineType, fileName);
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

        return text;
    }
}
