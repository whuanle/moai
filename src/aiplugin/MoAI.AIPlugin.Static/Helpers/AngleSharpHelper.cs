using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace MoAI.AIPlugin.Static.Helpers;

/// <summary>
/// AngleSharp 网页文本提取辅助方法.
/// </summary>
public static class AngleSharpHelper
{
    /// <summary>
    /// 需要忽略的标签（脚本、样式等非正文内容）.
    /// </summary>
    private static readonly HashSet<string> _ignoredTags = new(StringComparer.OrdinalIgnoreCase)
    { "script", "style", "meta", "link", "noscript" };

    /// <summary>
    /// 块级标签，递归完成后需要补一个换行.
    /// </summary>
    private static readonly HashSet<string> _blockTags = new(StringComparer.OrdinalIgnoreCase)
    { "p", "h1", "h2", "h3", "h4", "h5", "h6", "div", "section", "article", "ul", "ol", "li", "br", "hr", "header", "footer", "main", "aside" };

    /// <summary>
    /// 提取 HTML 文档中的有效文本内容.
    /// </summary>
    /// <param name="document">已解析的网页文档.</param>
    /// <returns>规范化后的纯文本内容.</returns>
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

    /// <summary>
    /// 递归处理单个节点及其子节点，把文本追加到缓冲区.
    /// </summary>
    /// <param name="node">当前节点.</param>
    /// <param name="textBuilder">文本缓冲区.</param>
    private static void ProcessNode(INode node, StringBuilder textBuilder)
    {
        if (node is IElement element)
        {
            // 忽略不需要的标签
            if (_ignoredTags.Contains(element.LocalName))
            {
                return;
            }

            // 特殊标签处理
            switch (element.LocalName.ToLowerInvariant())
            {
                case "pre":
                    textBuilder.AppendLine(element.TextContent); // 保留 <pre> 内部所有格式
                    return; // 已处理，不再递归子节点
                case "li":
                    textBuilder.Append("\n• "); // 为列表项添加项目符号并换行
                    break;
                case "img":
                    var alt = element.GetAttribute("alt");
                    if (!string.IsNullOrWhiteSpace(alt))
                    {
                        textBuilder.Append($"[图片: {alt}] ");
                    }

                    break;
                case "a":
                    var title = element.GetAttribute("title");
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
        if (node is IElement blockElement && _blockTags.Contains(blockElement.LocalName))
        {
            textBuilder.Append('\n');
        }
    }
}
