using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace MoAI.Storage.TextExtract;

/// <summary>
/// ODT（OpenDocument Text）文本提取：zip 容器 → content.xml → 按 text:h/text:p 元素逐段取文本.
/// 解析不可信 XML 前拒绝 DOCTYPE/ENTITY（不启用外部实体），DTD 声明出现即视为非法文档.
/// </summary>
public static class OdtTextExtractor
{
    /// <summary>
    /// OpenDocument 命名空间（正文段落元素 text:p/text:h 所属）.
    /// </summary>
    private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    /// <summary>
    /// content.xml 在 ODT zip 内的固定路径.
    /// </summary>
    private const string ContentXmlPath = "content.xml";

    /// <summary>
    /// 从流中提取 ODT 文本.
    /// </summary>
    /// <param name="stream">文件流（不要求可 Seek，内部整体缓冲）.</param>
    /// <param name="fileName">文件名，用于错误提示.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>按段落顺序拼接的正文文本.</returns>
    public static async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        using var archive = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: false);
        var contentEntry = archive.GetEntry(ContentXmlPath);
        if (contentEntry == null)
        {
            throw new InvalidOperationException($"文件缺少 {ContentXmlPath}，不是有效的 ODT 文档: {fileName}");
        }

        string xml;
        await using (var entryStream = contentEntry.Open())
        using (var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            xml = await reader.ReadToEndAsync(cancellationToken);
        }

        // 安全约束：不可信 XML 拒绝 DOCTYPE/ENTITY 声明（不启用外部实体），出现即拒绝提取
        var probeLength = Math.Min(xml.Length, 64 * 1024);
        if (xml.IndexOf("<!DOCTYPE", 0, probeLength, StringComparison.OrdinalIgnoreCase) >= 0
            || xml.IndexOf("<!ENTITY", 0, probeLength, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            throw new InvalidOperationException($"文档包含 DTD 声明，已拒绝解析: {fileName}");
        }

        var settings = new XmlReaderSettings
        {
            XmlResolver = null,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = false,
        };

        XDocument document;
        using (var xmlReader = XmlReader.Create(new StringReader(xml), settings))
        {
            document = XDocument.Load(xmlReader);
        }

        // 逐段取文本：text:h（标题）与 text:p（段落），表格单元格内的段落同样按行输出
        var sb = new StringBuilder();
        foreach (var paragraph in document.Descendants()
                     .Where(x => x.Name.LocalName is "p" or "h" && x.Name.NamespaceName == TextNamespace))
        {
            var line = paragraph.Value.Trim();
            if (line.Length > 0)
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString().Trim();
    }
}
