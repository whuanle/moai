using Microsoft.Extensions.DependencyInjection;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.AIPlugin.Services;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 文本工具集（多工具动态插件演示模板）测试：契约发现、按 toolKey 分发执行与参数护栏.
/// </summary>
public class TextToolkitPluginTests
{
    private readonly PluginExecutor _executor;

    public TextToolkitPluginTests()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        _executor = new PluginExecutor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new PluginRegistry());
    }

    [Fact]
    public void TryGetPluginInfo_DiscoversThreeTools()
    {
        var info = PluginTypeHelper.TryGetPluginInfo(typeof(TextToolkitPlugin));

        Assert.NotNull(info);
        Assert.True(info!.IsDynamic);
        Assert.True(info.IsMultiTool);
        Assert.Equal(3, info.Tools.Count);
        Assert.Null(info.Request);
        Assert.Null(info.Response);
        Assert.Equal(["slugify", "word_count", "reverse"], info.Tools.Select(x => x.Key).ToArray());
        Assert.All(info.Tools, t => Assert.NotEmpty(t.Title));
        Assert.All(info.Tools, t => Assert.False(string.IsNullOrWhiteSpace(t.ParamsExample)));
        Assert.NotNull(info.FindTool("SLUGIFY"));
        Assert.Null(info.FindTool("missing"));
    }

    [Fact]
    public void TryGetPluginInfo_SingleToolPluginKeepsLegacyShape()
    {
        var info = PluginTypeHelper.TryGetPluginInfo(typeof(DynamicGreetPlugin));

        Assert.NotNull(info);
        Assert.False(info!.IsMultiTool);
        Assert.Empty(info.Tools);
        Assert.NotNull(info.Request);
    }

    [Fact]
    public async Task ExecuteAsync_Slugify_ByToolKey()
    {
        var result = await _executor.ExecuteAsync(
            "text_toolkit",
            """{"Text":"Hello World 2!"}""",
            """{"MaxTextLength":10000}""",
            null,
            default,
            "slugify");

        Assert.True(result.Success, result.Error);
        Assert.Contains("hello-world-2", result.DataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WordCount_ByToolKey()
    {
        var result = await _executor.ExecuteAsync(
            "text_toolkit",
            """{"Text":"a b\nc"}""",
            """{"MaxTextLength":10000}""",
            null,
            default,
            "word_count");

        Assert.True(result.Success, result.Error);
        Assert.Contains("\"Chars\":4", result.DataJson, StringComparison.Ordinal);
        Assert.Contains("\"Words\":3", result.DataJson, StringComparison.Ordinal);
        Assert.Contains("\"Lines\":2", result.DataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Reverse_ByToolKey()
    {
        var result = await _executor.ExecuteAsync(
            "text_toolkit",
            """{"Text":"abc"}""",
            """{"MaxTextLength":10000}""",
            null,
            default,
            "reverse");

        Assert.True(result.Success, result.Error);
        Assert.Contains("\"Reversed\":\"cba\"", result.DataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownToolKey_Fails()
    {
        var result = await _executor.ExecuteAsync(
            "text_toolkit",
            """{"Text":"abc"}""",
            """{"MaxTextLength":10000}""",
            null,
            default,
            "no_such_tool");

        Assert.False(result.Success);
        Assert.Contains("工具不存在", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MissingToolKeyOnMultiTool_Fails()
    {
        var result = await _executor.ExecuteAsync(
            "text_toolkit",
            """{"Text":"abc"}""",
            """{"MaxTextLength":10000}""",
            null,
            default);

        Assert.False(result.Success);
        Assert.Contains("工具不存在", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_BadConfig_FailsBeforeRun()
    {
        var result = await _executor.ExecuteAsync(
            "text_toolkit",
            """{"Text":"abc"}""",
            """{"MaxTextLength":1}""",
            null,
            default,
            "reverse");

        Assert.False(result.Success);
        Assert.Contains("MaxTextLength", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TextOverConfigLimit_Rejected()
    {
        var result = await _executor.ExecuteAsync(
            "text_toolkit",
            $$"""{"Text":"{{new string('a', 300)}}"}""",
            """{"MaxTextLength":100}""",
            null,
            default,
            "reverse");

        Assert.False(result.Success);
        Assert.Contains("超过配置上限", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_ClampsNothingButValidates()
    {
        var plugin = new TextToolkitPlugin();
        Assert.Contains("MaxTextLength", await plugin.InitAsync(new TextToolkitConfig { MaxTextLength = 10 }), StringComparison.Ordinal);
        Assert.Null(await plugin.InitAsync(new TextToolkitConfig { MaxTextLength = 5000 }));
    }
}
