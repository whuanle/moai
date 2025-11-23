using AngleSharp.Dom;
using System.Text;
using System.Text.RegularExpressions;

namespace MoAI.Plugin.Helpers;

/// <summary>
/// AngleSharpHelper.
/// </summary>
public static class AngleSharpHelper
{
    /// <summary>
    /// 提取 html 中的有效文本内容.
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    public static string ExtractTextContent(IDocument document)
    {
        var textBuilder = new StringBuilder();

        // 提取标题
        if (!string.IsNullOrWhiteSpace(document.Title))
        {
            textBuilder.AppendLine($"标题: {document.Title.Trim()}");
            textBuilder.AppendLine("========================");
        }

        // 递归处理 Body 中的节点
        if (document.Body != null)
        {
            ProcessNode(document.Body, textBuilder);
        }

        // 清理和规范化文本，保留换行
        var lines = textBuilder.ToString().Split('\n');
        var cleanedLines = lines
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim()) // 替换行内多余空格并裁剪
            .Where(line => !string.IsNullOrWhiteSpace(line)); // 移除空行

        return string.Join(Environment.NewLine, cleanedLines);
    }

    // 使用 HashSet 提升性能
    private static readonly HashSet<string> IgnoredTags = new(StringComparer.OrdinalIgnoreCase)
{ "script", "style", "meta", "link", "noscript" };

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
{ "p", "h1", "h2", "h3", "h4", "h5", "h6", "div", "section", "article", "ul", "ol", "li", "br", "hr", "header", "footer", "main", "aside" };


    static void ProcessNode(INode node, StringBuilder textBuilder)
    {
        if (node is IElement element)
        {
            // 忽略不需要的标签
            if (IgnoredTags.Contains(element.LocalName))
            {
                return;
            }

            // 特殊标签处理
            switch (element.LocalName.ToLower())
            {
                case "pre":
                    textBuilder.AppendLine(element.TextContent); // 保留 <pre> 内部所有格式
                    return; // 已处理，不再递归子节点
                case "li":
                    textBuilder.Append("\n• "); // 为列表项添加项目符号并换行
                    break;
                case "img":
                    string alt = element.GetAttribute("alt");
                    if (!string.IsNullOrWhiteSpace(alt))
                    {
                        textBuilder.Append($"[图片: {alt}] ");
                    }
                    break;
                case "a":
                    string title = element.GetAttribute("title");
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        textBuilder.Append($"[链接标题: {title}] ");
                    }
                    break;
            }
        }
        else if (node is IText textNode)
        {
            // 提取文本节点内容
            if (!string.IsNullOrWhiteSpace(textNode.TextContent))
            {
                textBuilder.Append(textNode.TextContent);
            }
        }

        // 递归处理子节点
        foreach (var child in node.ChildNodes)
        {
            ProcessNode(child, textBuilder);
        }

        // 为块级元素在末尾添加换行，以分隔内容
        if (node is IElement blockElement && BlockTags.Contains(blockElement.LocalName))
        {
            textBuilder.Append("\n");
        }
    }
}
