using System.Threading;
using System.Threading.Tasks;
using Markdig;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// Markdown 转 HTML（静态插件）：把 Markdown 文本转换为 HTML.
/// </summary>
[AiPlugin(key: "static_markdown_to_html", Name = "markdown转html", Description = "将 markdown 转换为 html，消息内容格式为普通文本")]
public class MarkdownToHtmlPlugin : IStaticPluginRuntime<MarkdownToHtmlRequest, MarkdownToHtmlResponse>
{
    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
            "Markdown": "### 这是一个标题\n> **注意**: 这是一个引用文本示例\n- 列表项 1\n- 列表项 2\n- 列表项 3"
            }
            """;
    }

    /// <inheritdoc/>
    public Task<MarkdownToHtmlResponse> RunAsync(MarkdownToHtmlRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new MarkdownToHtmlResponse
        {
            Html = Markdown.ToHtml(request.Markdown ?? string.Empty),
        });
    }
}
