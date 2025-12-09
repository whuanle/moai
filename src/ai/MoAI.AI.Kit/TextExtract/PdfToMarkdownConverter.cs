using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.Tokens;

namespace MoAI.AI.TextExtract;

/// <summary>
/// 读取 pdf 转换为 markdown 格式.
/// </summary>
public static class PdfToMarkdownConverter
{
    public static string ConvertToMarkdown(string pdfPath)
    {
        var markdown = new StringBuilder();

        using (var document = PdfDocument.Open(pdfPath))
        {
            foreach (var page in document.GetPages())
            {
                markdown.AppendLine(ConvertPageToMarkdown(page));
                markdown.AppendLine();
            }
        }

        return markdown.ToString();
    }

    private static string ConvertPageToMarkdown(Page page)
    {
        var markdown = new StringBuilder();

        // ✅ 关键：使用 GetWords() 而不是 Letters
        var words = page.GetWords();

        // 使用 Docstrum 分割文本块
        var textBlocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);

        // 按阅读顺序排序
        var orderedBlocks = UnsupervisedReadingOrderDetector.Instance.Get(textBlocks);

        // 处理文本块
        foreach (var block in orderedBlocks)
        {
            ProcessTextBlock(markdown, block);
        }

        // 处理图像
        int imageIndex = 0;
        foreach (var image in page.GetImages())
        {
            // todo: 暂时不处理图片.
            //var imagePath = SaveImage(image, page.Number, imageIndex++);
            //markdown.AppendLine($"![Image]({imagePath})");

            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private static void ProcessTextBlock(StringBuilder markdown, TextBlock block)
    {
        if (block.TextLines.Count == 0) return;

        // 获取字体信息
        var firstWord = block.TextLines.First().Words.FirstOrDefault();
        if (firstWord == null || firstWord.Letters.Count == 0) return;

        var firstLetter = firstWord.Letters.First();
        var fontSize = firstLetter.PointSize;
        var fontName = firstLetter.FontName;

        // 根据字体大小判断标题级别
        if (fontSize >= 20)
        {
            markdown.AppendLine($"# {block.Text}");
        }
        else if (fontSize >= 16)
        {
            markdown.AppendLine($"## {block.Text}");
        }
        else if (fontSize >= 14)
        {
            markdown.AppendLine($"### {block.Text}");
        }
        else
        {
            // 检查粗体和斜体
            bool isBold = fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
            bool isItalic = fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase);

            if (isBold && isItalic)
            {
                markdown.AppendLine($"***{block.Text}***");
            }
            else if (isBold)
            {
                markdown.AppendLine($"**{block.Text}**");
            }
            else if (isItalic)
            {
                markdown.AppendLine($"*{block.Text}*");
            }
            else
            {
                markdown.AppendLine(block.Text);
            }
        }

        markdown.AppendLine(); // 段落间换行
    }

    public static string SaveImage(IPdfImage image, int pageNumber, int imageIndex)
    {
        var outputDir = "images";
        Directory.CreateDirectory(outputDir);

        var filename = $"page_{pageNumber}_image_{imageIndex}";
        string filepath;

        // 优先尝试保存为PNG（最通用的格式）
        if (image.TryGetPng(out var pngBytes))
        {
            filepath = Path.Combine(outputDir, filename + ".png");
            File.WriteAllBytes(filepath, pngBytes);
            return filepath;
        }

        // 检查是否为JPEG（直接嵌入的JPEG文件）
        // 根据ImageDictionary判断过滤器类型
        if (image.ImageDictionary.TryGet(NameToken.Filter, out var filterToken))
        {
            var filterName = GetFilterName(filterToken);

            if (filterName == "DCTDecode" || filterName == "DCT")
            {
                // 这是JPEG图像，RawBytes直接就是有效的JPEG文件
                filepath = Path.Combine(outputDir, filename + ".jpg");
                File.WriteAllBytes(filepath, image.RawBytes.ToArray());
                return filepath;
            }

            if (filterName == "JPXDecode")
            {
                // JPEG2000格式
                filepath = Path.Combine(outputDir, filename + ".jp2");
                File.WriteAllBytes(filepath, image.RawBytes.ToArray());
                return filepath;
            }
        }

        // 尝试获取解码后的字节
        if (image.TryGetBytesAsMemory(out var decodedBytes))
        {
            // 根据ColorSpace和BitsPerComponent转换为常见格式
            filepath = Path.Combine(outputDir, filename + ".raw");
            File.WriteAllBytes(filepath, decodedBytes.ToArray());

            // 可以考虑使用图像处理库（如ImageSharp）转换为PNG
            // TryConvertToPng(decodedBytes, image, out var convertedPath);

            return filepath;
        }

        // 最后的fallback：保存原始字节
        filepath = Path.Combine(outputDir, filename + ".raw");
        File.WriteAllBytes(filepath, image.RawBytes.ToArray());
        return filepath;
    }

    private static string GetFilterName(IToken filterToken)
    {
        if (filterToken is NameToken nameToken)
        {
            return nameToken.Data;
        }

        if (filterToken is ArrayToken arrayToken && arrayToken.Data.Count > 0)
        {
            // 使用第一个过滤器
            if (arrayToken.Data[0] is NameToken firstFilter)
            {
                return firstFilter.Data;
            }
        }

        return string.Empty;
    }
}