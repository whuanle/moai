using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace MoAI.Storage.TextExtract;

/// <summary>
/// EPUB 电子书文本提取：zip 容器 → container.xml 定位 OPF → 按 spine 顺序读取 xhtml/html 章节 → 逐章节抽取正文.
/// OPF/容器解析失败时回退为按文件名排序遍历全部 xhtml/html 条目，保证内容仍可提取.
/// </summary>
public static class EpubTextExtractor
{
    /// <summary>
    /// 从流中提取 EPUB 文本.
    /// </summary>
    /// <param name="stream">文件流（不要求可 Seek，内部整体缓冲）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>按章节顺序拼接的正文文本.</returns>
    public static async Task<string> ExtractAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        using var archive = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: false);
        var entries = archive.Entries
            .Where(x => x.FullName != string.Empty && x.Name != string.Empty)
            .ToDictionary(x => x.FullName, StringComparer.OrdinalIgnoreCase);

        // 1. container.xml 定位 OPF
        var opfPath = TryResolveOpfPath(entries);

        // 2. OPF spine 章节顺序；解析失败回退为按名排序的全部 xhtml/html 条目
        var chapterPaths = opfPath != null && entries.TryGetValue(opfPath, out var opfEntry)
            ? TryResolveSpineChapters(opfEntry, entries)
            : null;
        chapterPaths ??= entries.Keys
            .Where(x => IsChapterExtension(Path.GetExtension(x)))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // 3. 逐章节提取正文
        var parser = new HtmlParser();
        var sb = new StringBuilder();
        foreach (var chapterPath in chapterPaths)
        {
            if (!entries.TryGetValue(chapterPath, out var entry))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string html;
            await using (var entryStream = entry.Open())
            using (var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                html = await reader.ReadToEndAsync(cancellationToken);
            }

            var chapterText = ExtractChapterText(parser, html);
            if (!string.IsNullOrWhiteSpace(chapterText))
            {
                if (sb.Length > 0)
                {
                    sb.Append("\n\n");
                }

                sb.Append(chapterText);
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// 章节/OPF 引用的扩展名（xhtml 为主，兼容 html）.
    /// </summary>
    private static bool IsChapterExtension(string extension)
    {
        return extension is ".xhtml" or ".html" or ".htm";
    }

    /// <summary>
    /// 从 META-INF/container.xml 解析 rootfile full-path（OPF 位置）；缺失或解析失败返回 null.
    /// </summary>
    private static string? TryResolveOpfPath(IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        if (!entries.TryGetValue("META-INF/container.xml", out var containerEntry))
        {
            return null;
        }

        try
        {
            var xml = ReadEntryText(containerEntry);
            var document = new HtmlParser().ParseDocument(xml);
            var rootfile = document.QuerySelector("rootfile");
            var fullPath = rootfile?.GetAttribute("full-path");
            return string.IsNullOrWhiteSpace(fullPath) ? null : fullPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 读取 zip 条目为 UTF-8 文本.
    /// </summary>
    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 从 OPF 的 manifest + spine 解析章节阅读顺序（相对 href 以 OPF 所在目录解析）.
    /// </summary>
    private static List<string>? TryResolveSpineChapters(ZipArchiveEntry opfEntry, IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        try
        {
            var opfXml = ReadEntryText(opfEntry);
            var document = new HtmlParser().ParseDocument(opfXml);

            // manifest: item[id][href][media-type]，仅保留章节类文档
            var idToPath = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in document.QuerySelectorAll("manifest > item"))
            {
                var id = item.GetAttribute("id");
                var href = item.GetAttribute("href");
                var mediaType = item.GetAttribute("media-type") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var isChapter = mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase)
                    || mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                    || IsChapterExtension(Path.GetExtension(href));
                if (isChapter)
                {
                    idToPath[id] = ResolveZipPath(opfEntry.FullName, href);
                }
            }

            // spine: itemref[idref] 阅读顺序
            var result = new List<string>();
            foreach (var itemref in document.QuerySelectorAll("spine > itemref"))
            {
                var idref = itemref.GetAttribute("idref");
                if (idref != null && idToPath.TryGetValue(idref, out var path) && !result.Contains(path))
                {
                    result.Add(path);
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 以 OPF 所在目录为基准解析 zip 内相对路径（处理 ./ 与 ../ 段）.
    /// </summary>
    private static string ResolveZipPath(string basePath, string relativePath)
    {
        var cleanRelative = relativePath.Split('#')[0];
        var baseDirectory = basePath.Contains('/')
            ? basePath[..(basePath.LastIndexOf('/'))]
            : string.Empty;

        var segments = new List<string>();
        if (!string.IsNullOrEmpty(baseDirectory))
        {
            segments.AddRange(baseDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var segment in cleanRelative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    /// <summary>
    /// 单章节正文抽取：DOM 按块级标签插入换行后取文本，脚本/样式/头信息跳过.
    /// </summary>
    private static string ExtractChapterText(HtmlParser parser, string html)
    {
        IDocument document;
        try
        {
            document = parser.ParseDocument(html);
        }
        catch
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var body = document.Body ?? (INode)document.DocumentElement;
        AppendNode(body, sb);

        // 压缩连续空行，块级标签换行已足够表达段落结构
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n").Trim();
    }

    private static void AppendNode(INode node, StringBuilder sb)
    {
        if (node is IElement element)
        {
            var tag = element.LocalName?.ToLowerInvariant() ?? string.Empty;
            if (tag is "script" or "style" or "head" or "template")
            {
                return;
            }

            if (tag == "br")
            {
                sb.Append('\n');
                return;
            }

            foreach (var child in element.ChildNodes)
            {
                AppendNode(child, sb);
            }

            // 块级元素结束补换行，形成段落边界
            if (tag is "p" or "div" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                or "li" or "tr" or "blockquote" or "pre" or "figcaption" or "section" or "article" or "aside" or "table")
            {
                sb.Append('\n');
            }

            return;
        }

        if (node is IText textNode)
        {
            sb.Append(textNode.Data);
        }
    }
}
