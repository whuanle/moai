using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 文本工具集（多工具动态插件演示模板）：一个模板经多个 <c>[AiPluginTool]</c> 方法对外暴露 slugify/word_count/reverse 三个工具，
/// 每个工具可独立调试（运行测试传 toolKey）并拥有独立的参数运行示例.
/// </summary>
[AiPlugin(key: "text_toolkit", Name = "文本工具集", Description = "多工具模板：slugify 生成 URL 友好 slug、word_count 统计字符/词/行、reverse 反转文本")]
public class TextToolkitPlugin : IDynamicPluginToolsRuntime<TextToolkitConfig>
{
    private const int MinTextLength = 100;
    private const int MaxTextLengthLimit = 1000000;

    private TextToolkitConfig? _config;

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
            "MaxTextLength": 10000 // 单次处理的最大文本长度（字符数）
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(TextToolkitConfig config)
    {
        if (config.MaxTextLength is < MinTextLength or > MaxTextLengthLimit)
        {
            return Task.FromResult<string?>($"MaxTextLength 必须在 {MinTextLength}-{MaxTextLengthLimit} 之间");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// slugify：文本转 URL 友好 slug（小写、非字母数字转分隔符、合并连续分隔符、去首尾）.
    /// </summary>
    /// <param name="request">请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>slug.</returns>
    [AiPluginTool(key: "slugify", Title = "生成 Slug", Description = "把文本转换为 URL 友好的 slug：转小写、非字母数字字符替换为分隔符、合并连续分隔符并去掉首尾分隔符。")]
    public Task<TextToolkitSlugifyResponse> SlugifyAsync(TextToolkitSlugifyRequest request, CancellationToken cancellationToken)
    {
        EnsureLength(request.Text);

        var separator = string.Equals(request.Separator, "_", StringComparison.Ordinal) ? "_" : "-";
        var normalized = new string(request.Text.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray());

        var segments = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Task.FromResult(new TextToolkitSlugifyResponse { Slug = string.Join(separator, segments) });
    }

    /// <summary>
    /// word_count：统计字符数/词数/行数.
    /// </summary>
    /// <param name="request">请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>统计结果.</returns>
    [AiPluginTool(key: "word_count", Title = "文本统计", Description = "统计文本的字符数（不含换行）、词数（按空白切分）与行数。")]
    public Task<TextToolkitWordCountResponse> WordCountAsync(TextToolkitWordCountRequest request, CancellationToken cancellationToken)
    {
        EnsureLength(request.Text);

        var text = request.Text;
        var lines = text.Length == 0 ? 0 : text.Split('\n').Length;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var chars = text.Count(c => c != '\n' && c != '\r');
        return Task.FromResult(new TextToolkitWordCountResponse { Chars = chars, Words = words, Lines = lines });
    }

    /// <summary>
    /// reverse：反转文本（按字符反转）.
    /// </summary>
    /// <param name="request">请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>反转结果.</returns>
    [AiPluginTool(key: "reverse", Title = "反转文本", Description = "把输入文本按字符反转后返回。")]
    public Task<TextToolkitReverseResponse> ReverseAsync(TextToolkitReverseRequest request, CancellationToken cancellationToken)
    {
        EnsureLength(request.Text);

        var array = request.Text.ToCharArray();
        Array.Reverse(array);
        return Task.FromResult(new TextToolkitReverseResponse { Reversed = new string(array) });
    }

    private void EnsureLength(string text)
    {
        if (text.Length > (_config?.MaxTextLength ?? MinTextLength))
        {
            throw new BusinessException($"文本长度超过配置上限 {_config?.MaxTextLength}") { StatusCode = 400 };
        }
    }
}
