#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;
using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Helpers;
using MoAI.Plugin.Models;
using System.ComponentModel;
using System.Text;

namespace MoAI.Plugin.Plugins.WebContentFetch;

/// <summary>
/// 网页内容抓取插件.
/// </summary>
[Attributes.NativePluginConfig(
    "web_content_fetch",
    Name = "网页内容抓取",
    Description = "获取指定网页链接的内容",
    Classify = NativePluginClassify.Tool,
    ParamType = typeof(WebContentFetchParams))]
[Description("获取指定网页链接的内容")]
[InjectOnTransient]
public class WebContentFetchPlugin : IToolPluginRuntime
{
    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
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
    [Description("获取指定网页链接的内容，默认开启extractText，否则页面内容非常大")]
    public async Task<string> InvokeAsync([Description("目标网页链接")] string url, [Description("是否提取html中的纯文本内容")] bool extractText = true)
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

            var result = AngleSharpHelper.ExtractTextContent(document);
            return result;
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
}
