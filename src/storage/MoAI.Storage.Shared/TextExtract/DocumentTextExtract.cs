using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Infra;

namespace MoAI.Storage.TextExtract;

/// <summary>
/// 文档文本提取路由门面：Maomi.ToMarkdown 原生扩展名之外的格式（EPUB/ODT/RTF/代码配置类文本）统一走平台自研提取.
/// 调用方（知识库/知识图谱/对话附件）先判 <see cref="IsNativeSupported"/>，否则调用 <see cref="ExtractAsync"/>.
/// </summary>
public static class DocumentTextExtract
{
    /// <summary>
    /// 判断文件名是否为 Maomi.ToMarkdown 原生支持的扩展名（pdf/docx/xlsx/pptx/html/md/txt/json 等 10 种）.
    /// </summary>
    /// <param name="fileName">文件名.</param>
    /// <returns>是否原生支持.</returns>
    public static bool IsNativeSupported(string fileName)
    {
        return DocumentFormatConst.NativeFormats.Contains(Path.GetExtension(fileName).ToLowerInvariant());
    }

    /// <summary>
    /// 提取非原生格式的文本：EPUB/ODT/RTF 走自研提取器，其余按纯文本兜底（NUL 嗅探拒绝二进制）.
    /// </summary>
    /// <param name="stream">文件流（不要求可 Seek）.</param>
    /// <param name="fileName">文件名，用于提取器分派与错误提示.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>提取出的文本.</returns>
    /// <exception cref="InvalidOperationException">二进制文件或损坏的容器文档.</exception>
    public static async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".epub" => await EpubTextExtractor.ExtractAsync(stream, cancellationToken),
            ".odt" => await OdtTextExtractor.ExtractAsync(stream, fileName, cancellationToken),
            ".rtf" => await RtfTextExtractor.ExtractAsync(stream, fileName, cancellationToken),
            _ => await TextFileReader.ReadAsTextAsync(stream, fileName, cancellationToken),
        };
    }
}
