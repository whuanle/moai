using Maomi;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.Image;
using Microsoft.KernelMemory.DataFormats.Office;
using Microsoft.KernelMemory.DataFormats.Pdf;
using Microsoft.KernelMemory.DataFormats.Text;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.Text;
using System.Text;

namespace MoAI.Infra.Handlers;

[InjectOnTransient]
public class TextExtractionFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public TextExtractionFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    private readonly Dictionary<string, IContentDecoder> _defaultDecoders;

    public IReadOnlyCollection<IContentDecoder> Decoders => _defaultDecoders.Values;

    public TextExtractionFactory() : this(DefaultLogger.Factory)
    {
        _defaultDecoders = new Dictionary<string, IContentDecoder>(StringComparer.OrdinalIgnoreCase);

        RegisterImageDecoder();
        RegisterMsExcelDecoder();
        RegisterMsPowerPointDecoder();
        RegisterMsWordDecoder();
        RegisterPdfDecoder();
        RegisterMarkDownDecoder();
        RegisterTextDecoder();
        RegisterHtmlDecoder();
    }

    /// <summary>
    /// ImageDecoder.
    /// </summary>
    /// <param name="ocrEngine"></param>
#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
    public void RegisterImageDecoder(IOcrEngine? ocrEngine = null)
    {
        _defaultDecoders[nameof(ImageDecoder)] = new ImageDecoder(ocrEngine, _loggerFactory);
    }

    /// <summary>
    /// MsExcelDecoder.
    /// </summary>
    /// <param name="decoderConfig"></param>
    public void RegisterMsExcelDecoder(MsExcelDecoderConfig? decoderConfig = null)
    {
        _defaultDecoders[nameof(MsExcelDecoder)] = new MsExcelDecoder(decoderConfig, _loggerFactory);
    }

    /// <summary>
    /// MsPowerPointDecoder.
    /// </summary>
    /// <param name="decoderConfig"></param>
    public void RegisterMsPowerPointDecoder(MsPowerPointDecoderConfig? decoderConfig = null)
    {
        _defaultDecoders[nameof(MsPowerPointDecoder)] = new MsPowerPointDecoder(decoderConfig, _loggerFactory);
    }

    /// <summary>
    /// MsWordDecoder.
    /// </summary>
    public void RegisterMsWordDecoder()
    {
        _defaultDecoders[nameof(MsWordDecoder)] = new MsWordDecoder(_loggerFactory);
    }

    /// <summary>
    /// PdfDecoder.
    /// </summary>
    public void RegisterPdfDecoder()
    {
        _defaultDecoders[nameof(PdfDecoder)] = new PdfDecoder(_loggerFactory);
    }

    /// <summary>
    /// MarkDownDecoder.
    /// </summary>
    public void RegisterMarkDownDecoder()
    {
        _defaultDecoders[nameof(MarkDownDecoder)] = new MarkDownDecoder(_loggerFactory);
    }

    /// <summary>
    /// TextDecoder.
    /// </summary>
    public void RegisterTextDecoder()
    {
        _defaultDecoders[nameof(TextDecoder)] = new TextDecoder(_loggerFactory);
    }

    /// <summary>
    /// HtmlDecoder.
    /// </summary>
    public void RegisterHtmlDecoder()
    {
        _defaultDecoders[nameof(HtmlDecoder)] = new HtmlDecoder(_loggerFactory);
    }
}
