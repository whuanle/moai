using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using MoAI.Database.Entities;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using Moq;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// GraphAppToolProvider：知识图谱工具生成与调用.
/// </summary>
public class GraphAppToolProviderTests
{
    private static AppAgentBuildContext Context(params long[] graphIds) => new()
    {
        App = new AppEntity(),
        AppId = System.Guid.NewGuid(),
        TeamId = 1,
        GraphIds = graphIds,
    };

    private static GraphSearchResult Result(params GraphSearchHit[] hits) => new(
        new List<GraphSearchHit>(hits),
        new List<string>(),
        string.Empty,
        new List<string>());

    [Fact]
    public async Task GetTools_NoGraph_ReturnsEmpty()
    {
        var provider = new GraphAppToolProvider(Mock.Of<IGraphSearchService>());

        var tools = await provider.GetToolsAsync(Context(), CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task Invoke_SearchesAndReturnsHits()
    {
        var search = new Mock<IGraphSearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), "A 和 B 什么关系", It.IsAny<int>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(new GraphSearchHit(
                7,
                "node-1",
                "张三",
                "项目负责人",
                2,
                "Person",
                0.92,
                new List<GraphNeighbor> { new("负责", "out", "项目X", "一个试点项目") })));

        var provider = new GraphAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);
        var tool = Assert.Single(tools);
        Assert.Equal(GraphAppToolProvider.ToolName, tool.Name);
        Assert.Equal("graph", tool.Kind);

        var result = await tool.InvokeAsync("{\"query\":\"A 和 B 什么关系\"}", CancellationToken.None);

        Assert.True(result.Success);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        var hit = doc.RootElement.GetProperty("hits")[0];
        Assert.Equal(7, hit.GetProperty("graphId").GetInt64());
        Assert.Equal("node-1", hit.GetProperty("nodeId").GetString());
        Assert.Equal("张三", hit.GetProperty("name").GetString());
        Assert.Equal("Person", hit.GetProperty("entityType").GetString());
        Assert.Equal("项目负责人", hit.GetProperty("description").GetString());
        Assert.Equal(0.92, hit.GetProperty("score").GetDouble());
        var neighbor = hit.GetProperty("neighbors")[0];
        Assert.Equal("负责", neighbor.GetProperty("relation").GetString());
        Assert.Equal("out", neighbor.GetProperty("direction").GetString());
        Assert.Equal("项目X", neighbor.GetProperty("name").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("skipped").GetArrayLength());
    }

    [Fact]
    public async Task Invoke_MissingQuery_Fails()
    {
        var provider = new GraphAppToolProvider(Mock.Of<IGraphSearchService>());
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);

        var result = await tools[0].InvokeAsync("{}", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("query", result.Error, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_TopKOutOfRange_ClampedToValidRange()
    {
        var search = new Mock<IGraphSearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), 20, It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result());

        var provider = new GraphAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);

        await tools[0].InvokeAsync("{\"query\":\"x\",\"topK\":100}", CancellationToken.None);

        search.Verify(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), 20, It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("{\"query\":\"x\"}")]
    [InlineData("{\"query\":\"x\",\"topK\":\"5\"}")]
    public async Task Invoke_MissingOrInvalidTopK_UsesDefaultFive(string argsJson)
    {
        var search = new Mock<IGraphSearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), 5, It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result());

        var provider = new GraphAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);

        await tools[0].InvokeAsync(argsJson, CancellationToken.None);

        search.Verify(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), 5, It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invoke_HugeTopK_ClampedToMax()
    {
        var search = new Mock<IGraphSearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), 20, It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result());

        var provider = new GraphAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);

        await tools[0].InvokeAsync("{\"query\":\"x\",\"topK\":1000000000000}", CancellationToken.None);

        search.Verify(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), 20, It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invoke_LongDescriptions_TruncatesPayloadWithinBudget()
    {
        var longDescription = new string('描', 5000);
        var hits = Enumerable.Range(0, 12).Select(i => new GraphSearchHit(
            i + 1,
            $"node-{i}",
            $"实体{i}",
            longDescription,
            2,
            "Person",
            0.9,
            new List<GraphNeighbor>(Enumerable.Range(0, 3).Select(j => new GraphNeighbor("负责", "out", $"邻居{j}", longDescription)))))
            .ToList();
        var search = new Mock<IGraphSearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphSearchResult(hits, new List<string>(), string.Empty, new List<string>()));

        var provider = new GraphAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7, 8, 9), CancellationToken.None);

        var result = await tools[0].InvokeAsync("{\"query\":\"x\"}", CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data!.Length <= 16 * 1024 + 1024);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.True(doc.RootElement.GetProperty("hitsTruncated").GetBoolean());
        var hitCount = doc.RootElement.GetProperty("hits").GetArrayLength();
        Assert.True(hitCount >= 1);
        Assert.True(hitCount < hits.Count);
        Assert.Equal(hitCount, doc.RootElement.GetProperty("count").GetInt32());
    }
}
