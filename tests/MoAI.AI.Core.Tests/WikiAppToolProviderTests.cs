using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using MoAI.Database.Entities;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;
using Moq;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// WikiAppToolProvider：知识库工具生成与调用.
/// </summary>
public class WikiAppToolProviderTests
{
    private static AppAgentBuildContext Context(params long[] wikiIds) => new()
    {
        App = new AppEntity(),
        AppId = System.Guid.NewGuid(),
        TeamId = 1,
        WikiIds = wikiIds,
    };

    [Fact]
    public async Task GetTools_NoWiki_ReturnsEmpty()
    {
        var provider = new WikiAppToolProvider(Mock.Of<IWikiSearchService>());

        var tools = await provider.GetToolsAsync(Context(), CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task Invoke_SearchesAndReturnsHits()
    {
        var search = new Mock<IWikiSearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), "退款", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WikiSearchHit>
            {
                new() { WikiId = 7, DocumentId = 1, DocumentName = "售后手册", Content = "退款流程说明", Score = 0.9 },
            });

        var provider = new WikiAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);
        var tool = Assert.Single(tools);
        Assert.Equal(WikiAppToolProvider.ToolName, tool.Name);

        var result = await tool.InvokeAsync("{\"query\":\"退款\"}", CancellationToken.None);

        Assert.True(result.Success);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("售后手册", doc.RootElement.GetProperty("hits")[0].GetProperty("document").GetString());
    }

    [Fact]
    public async Task Invoke_MissingQuery_Fails()
    {
        var provider = new WikiAppToolProvider(Mock.Of<IWikiSearchService>());
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);

        var result = await tools[0].InvokeAsync("{}", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("query", result.Error, System.StringComparison.Ordinal);
    }
}
