#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using ModelContextProtocol.Protocol;
using System.ComponentModel;

namespace MoAI.Plugin.Plugins.MarkdownToHtml;

/// <summary>
/// Markdown 转 html.
/// </summary>
[Attributes.NativePluginFieldConfigAttribute(
    "markdown_to_html",
    Name = "markdown转html",
    Description = "将 markdown 转换为 html，消息内容格式为普通文本",
    Classify = NativePluginClassify.Tool)]
[Description("将 markdown 转换为 html")]
[InjectOnTransient]
public class MarkdownToHtmlPlugin : IToolPluginRuntime
{
    /// <inheritdoc/>
    public async Task<string> GetParamsExampleValue()
    {
        await Task.CompletedTask;
        string example =
            """
            ### 这是一个标题
            > **注意**: 这是一个引用文本示例
            - 列表项 1
            - 列表项 2
            - 列表项 3
            """;

        return System.Text.Json.JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <summary>
    /// 读取服务当前本地时间.
    /// </summary>
    /// <param name="markdown"></param>
    /// <returns></returns>
    [KernelFunction("invoke")]
    [Description("将markdown转换为html")]
    public async Task<string> InvokeAsync([Description("markdown内容")]string markdown)
    {
        await Task.CompletedTask;
        return Markdig.Markdown.ToHtml(markdown);
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var str = System.Text.Json.JsonSerializer.Deserialize<string>(@params);
            var result = await InvokeAsync(str!);
            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }
}
