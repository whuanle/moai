using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.AI;
using MoAI.AI.Services;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 前端展示工具：ui_ 前缀三工具注册、桩执行固定结果与按请求头开关装配.
/// </summary>
public class FrontendToolContextProviderTests
{
    [Fact]
    public async Task BuildAIContext_RegistersThreeUiToolsWithGuide()
    {
        var provider = new FrontendToolContextProvider();

        var context = await provider.BuildAIContextAsync();

        var names = context.Tools.OfType<AIFunction>().Select(t => t.Name).ToList();
        Assert.Contains(FrontendToolContract.ShowDocumentToolName, names);
        Assert.Contains(FrontendToolContract.ShowCodeToolName, names);
        Assert.Contains(FrontendToolContract.ShowChartToolName, names);
        Assert.Equal(3, names.Count);
        Assert.All(names, name => Assert.True(FrontendToolContract.IsFrontendTool(name)));
        Assert.NotNull(context.Instructions);
        Assert.Contains("ui_show_document", context.Instructions);
        Assert.Contains("不要在正文重复完整内容", context.Instructions);
    }

    [Theory]
    [InlineData(FrontendToolContract.ShowDocumentToolName)]
    [InlineData(FrontendToolContract.ShowCodeToolName)]
    [InlineData(FrontendToolContract.ShowChartToolName)]
    public async Task Invoke_ReturnsFixedStubResult_WithoutSideEffects(string toolName)
    {
        var provider = new FrontendToolContextProvider();
        var context = await provider.BuildAIContextAsync();
        var tool = context.Tools.OfType<AIFunction>().Single(t => t.Name == toolName);

        // 模型传字符串参数（正常形态）与对象参数（不遵守 schema 的防御形态）都不得抛异常；
        // MEAI 编组会把 string 返回值包成 JsonElement，断言按文本比较
        var resultByString = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["title"] = "标题",
            ["content"] = "# 内容",
            ["code"] = "print(1)",
            ["language"] = "python",
            ["option"] = """{"series":[{"type":"bar","data":[1,2]}]}""",
        });
        Assert.Equal(FrontendToolContract.StubResultJson, resultByString?.ToString());

        var resultByObject = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["title"] = "标题",
            ["content"] = new[] { 1, 2 },
            ["option"] = new { series = new[] { new { type = "bar", data = new[] { 1, 2 } } } },
        });
        Assert.Equal(FrontendToolContract.StubResultJson, resultByObject?.ToString());

        // 模型漏传可选字段（如 ui_show_code 不带 language）也不得因必填校验中断
        var resultMinimal = await tool.InvokeAsync(new AIFunctionArguments { ["title"] = "标题" });
        Assert.Equal(FrontendToolContract.StubResultJson, resultMinimal?.ToString());
    }

    [Fact]
    public async Task Contributor_DisabledByDefault_EnabledOnlyWithFlag()
    {
        var contributor = new FrontendToolContextProviderContributor();

        var disabled = await contributor.CreateAsync(new AppAgentBuildContext(), CancellationToken.None);
        Assert.Null(disabled);

        var enabled = await contributor.CreateAsync(new AppAgentBuildContext { EnableUiTools = true }, CancellationToken.None);
        Assert.IsType<FrontendToolContextProvider>(enabled);
    }

    [Theory]
    [InlineData("ui_show_document", true)]
    [InlineData("ui_show_chart", true)]
    [InlineData("call_tool", false)]
    [InlineData(null, false)]
    public void IsFrontendTool_MatchesPrefixOnly(string? name, bool expected)
    {
        Assert.Equal(expected, FrontendToolContract.IsFrontendTool(name));
    }
}
