#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;
using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Models;
using System.ComponentModel;
using System.Text;

namespace MoAI.Plugin.Plugins.WebContentFetch;

/// <summary>
/// 网页内容抓取插件.
/// </summary>
[Attributes.NativePluginFieldConfig(
    "web_content_fetch",
    Name = "网页内容抓取",
    Description = "获取指定网页链接的内容",
    Classify = NativePluginClassify.Tool)]
[Description("获取指定网页链接的内容")]
[InjectOnTransient]
public class WebContentFetchPlugin : IToolPluginRuntime
{
    /// <inheritdoc/>
    public async Task<string> GetParamsExampleValue()
    {
        await Task.CompletedTask;
        var example = new WebContentFetchParams
        {
            Url = "https://example.com" // 示例：目标网页链接
        };

        return System.Text.Json.JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <summary>
    /// 获取网页内容.
    /// </summary>
    /// <param name="url">目标网页链接</param>
    /// <param name="extractText"></param>
    /// <returns>网页内容</returns>
    [KernelFunction("invoke")]
    [Description("获取指定网页链接的内容")]
    public async Task<string> InvokeAsync([Description("目标网页链接")] string url, [Description("是否提取html中的纯文本内容")] bool extractText = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new BusinessException("URL 不能为空");
        }

        try
        {
            var googleBotUserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
            var requester = new DefaultHttpRequester(googleBotUserAgent);

            // 避免抓取视频和图片等大资源
            var config = Configuration.Default
                .WithDefaultLoader(new LoaderOptions
                {
                    IsResourceLoadingEnabled = false,
                    Filter = request =>
                    {
                        return true;
                    }
                    //Filter = request =>
                    //{
                    //    var url = request.Address.Href;
                    //    return url.EndsWith(".css", StringComparison.InvariantCultureIgnoreCase) ||
                    //           url.EndsWith(".js", StringComparison.InvariantCultureIgnoreCase) ||
                    //           url.EndsWith(".woff", StringComparison.InvariantCultureIgnoreCase) ||
                    //           url.EndsWith(".woff2", StringComparison.InvariantCultureIgnoreCase) ||
                    //           url.EndsWith(".ttf", StringComparison.InvariantCultureIgnoreCase) ||
                    //           url.EndsWith(".eot", StringComparison.InvariantCultureIgnoreCase) ||
                    //           url.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase);
                    //}
                }).WithDefaultCookies()
                // .WithJs() // 只做静态页面抓取
                .With(requester);

            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(url, cancellationTokenSource.Token);

            if (document == null)
            {
                throw new BusinessException("无法加载网页内容");
            }

            // 返回网页的 HTML 内容
            if (extractText == false)
            {
                return document.DocumentElement.OuterHtml;
            }

            return ExtractTextContent(document);
        }
        catch (Exception ex)
        {
            throw new BusinessException($"抓取网页内容失败: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var input = System.Text.Json.JsonSerializer.Deserialize<WebContentFetchParams>(@params);
            var result = await InvokeAsync(input!.Url, input.ExtractText);
            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    private static string ExtractTextContent(IDocument document)
    {
        // 使用StringBuilder来高效构建文本
        StringBuilder textBuilder = new StringBuilder();

        try
        {
            // 提取meta标签信息，特别是description
            var metaTags = document.Head?.QuerySelectorAll("meta");
            if (metaTags != null)
            {
                foreach (var metaTag in metaTags)
                {
                    var name = metaTag.GetAttribute("name")?.ToLower();
                    var property = metaTag.GetAttribute("property")?.ToLower();
                    var content = metaTag.GetAttribute("content");

                    if (!string.IsNullOrEmpty(content))
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            textBuilder.AppendLine($"{name}: {content}");
                        }
                        else if (!string.IsNullOrEmpty(property))
                        {
                            textBuilder.AppendLine($"{property}: {content}");
                        }
                    }
                }
            }

            // 提取标题
            var title = document.Title;
            if (!string.IsNullOrEmpty(title))
            {
                textBuilder.AppendLine(title);
            }

            // 过滤掉不需要的元素
            var elements = document?.All.Where(e =>
                e.NodeType == AngleSharp.Dom.NodeType.Element &&
                e.LocalName != "script" &&
                e.LocalName != "style" &&
                e.LocalName != "noscript" &&
                !string.IsNullOrWhiteSpace(e.TextContent));

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    try
                    {
                        var text = element.TextContent?.Trim();
                        if (!string.IsNullOrEmpty(text) && element.ChildElementCount == 0)
                        {
                            textBuilder.AppendLine(text);
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单个元素处理错误，继续处理其他元素
                        continue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 如果处理出错，记录错误并尝试提取整个文档的文本
            textBuilder.Clear();
            textBuilder.AppendLine($"提取文本时发生错误: {ex.Message}");
            textBuilder.AppendLine(document.Body?.TextContent?.Trim() ?? "");
        }

        return textBuilder.ToString();
    }
}
