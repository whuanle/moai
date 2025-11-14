#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins;
using ModelContextProtocol.Protocol;
using System.ComponentModel;

namespace MoAI.Plugin.Tools.MarkdownToHtml;

/// <summary>
/// Markdown 转 html.
/// </summary>
[InternalPlugin(
    "markdown_to_html",
    Name = "markdown转html",
    Description = "将 markdown 转换为 html，消息内容格式为普通文本",
    RequiredConfiguration = false,
    Classify = InternalPluginClassify.Tool)]
[Description("将 markdown 转换为 html")]
[InjectOnTransient]
public class MarkdownToHtmlPlugin : IInternalPluginRuntime
{
    /// <inheritdoc/>
    public async Task<string?> CheckConfigAsync(string config)
    {
        await Task.CompletedTask;
        return string.Empty;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<InternalPluginParamConfig>> ExportConfigAsync()
    {
        await Task.CompletedTask;

        return Array.Empty<InternalPluginParamConfig>();
    }

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

    /// <inheritdoc/>
    public async Task ImportConfigAsync(string config)
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// 读取服务当前本地时间.
    /// </summary>
    /// <param name="markdown"></param>
    /// <returns></returns>
    [KernelFunction("send_message_to_feishu")]
    [Description("发送富文本文本消息到飞书")]
    public async Task<string> InvokeAsync(string markdown)
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
