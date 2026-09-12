using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// AppToolContextProvider 的渐进式工具披露行为.
/// </summary>
public class AppToolContextProviderTests
{
    private static AppTool MakeTool(string name, string? example = "{}", System.Func<string?, CancellationToken, Task<AppToolResult>>? invoke = null)
        => new()
        {
            Name = name,
            Title = name + "-title",
            Description = name + "-desc",
            Kind = "static",
            ParametersExample = example,
            InvokeAsync = invoke ?? ((_, _) => Task.FromResult(AppToolResult.Ok("{\"ok\":true}"))),
        };

    [Fact]
    public async Task BuildList_ListsAllTools()
    {
        var provider = new AppToolContextProvider([MakeTool("echo"), MakeTool("time")]);

        var json = await provider.BuildListJsonAsync(null);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
        var names = doc.RootElement.GetProperty("tools").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
        Assert.Contains("echo", names);
        Assert.Contains("time", names);
    }

    [Fact]
    public async Task BuildList_FiltersByQuery()
    {
        var provider = new AppToolContextProvider([MakeTool("echo"), MakeTool("weather")]);

        var json = await provider.BuildListJsonAsync("weather");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("weather", doc.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task BuildList_ResolvesLazyParametersExample()
    {
        var tool = new AppTool
        {
            Name = "mcp_tool",
            Title = "mcp",
            Description = "d",
            Kind = "mcp",
            ParametersExample = null,
            ResolveParametersExampleAsync = _ => Task.FromResult<string?>("{\"a\":1}"),
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{}")),
        };
        var provider = new AppToolContextProvider([tool]);

        var json = await provider.BuildListJsonAsync(null);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("{\"a\":1}", doc.RootElement.GetProperty("tools")[0].GetProperty("parametersExample").GetString());
    }

    [Fact]
    public async Task Invoke_CallsToolAndReturnsData()
    {
        string? received = null;
        var tool = MakeTool("echo", invoke: (args, _) => { received = args; return Task.FromResult(AppToolResult.Ok("{\"Message\":\"hi\"}")); });
        var provider = new AppToolContextProvider([tool]);

        var result = await provider.InvokeJsonAsync("echo", "{\"Name\":\"MoAI\"}");

        Assert.Equal("{\"Name\":\"MoAI\"}", received);
        Assert.Equal("{\"Message\":\"hi\"}", result);
    }

    [Fact]
    public async Task Invoke_UnknownTool_ReturnsErrorJson()
    {
        var provider = new AppToolContextProvider([MakeTool("echo")]);

        var result = await provider.InvokeJsonAsync("missing", "{}");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("missing", doc.RootElement.GetProperty("error").GetString(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_ToolFailure_ReturnsErrorJson()
    {
        var tool = MakeTool("bad", invoke: (_, _) => Task.FromResult(AppToolResult.Fail("boom")));
        var provider = new AppToolContextProvider([tool]);

        var result = await provider.InvokeJsonAsync("bad", "{}");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("boom", doc.RootElement.GetProperty("error").GetString());
    }
}
