using System.Collections.Generic;
using System.Linq;
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
    public async Task GetTools_WithWiki_ReturnsSearchAndChunkTools()
    {
        var provider = new WikiAppToolProvider(Mock.Of<IWikiSearchService>());

        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, x => x.Name == WikiAppToolProvider.ToolName);
        Assert.Contains(tools, x => x.Name == WikiAppToolProvider.ChunkToolName);
    }

    [Fact]
    public async Task Invoke_SearchesAndReturnsHitsWithContext()
    {
        var search = new Mock<IWikiSearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), "退款", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WikiSearchHit>
            {
                new()
                {
                    WikiId = 7,
                    DocumentId = 1,
                    DocumentName = "售后手册",
                    ChunkId = 100,
                    ChunkIndex = 4,
                    DocumentChunkCount = 12,
                    Content = "退款流程说明",
                    Score = 0.9,
                    RerankScore = 0.95,
                    Context = [new WikiSearchContextChunk { ChunkId = 99, ChunkIndex = 3, Content = "前一片段" }],
                },
            });

        var provider = new WikiAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);
        var tool = tools.Single(x => x.Name == WikiAppToolProvider.ToolName);

        var result = await tool.InvokeAsync("{\"query\":\"退款\"}", CancellationToken.None);

        Assert.True(result.Success);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        var hit = doc.RootElement.GetProperty("hits")[0];
        Assert.Equal("售后手册", hit.GetProperty("document").GetString());
        Assert.Equal(4, hit.GetProperty("chunkIndex").GetInt32());
        Assert.Equal(12, hit.GetProperty("totalChunks").GetInt32());
        Assert.Equal(0.95, hit.GetProperty("rerankScore").GetDouble());
        Assert.Equal("100", hit.GetProperty("chunkId").GetString());
        Assert.Equal(1, hit.GetProperty("context").GetArrayLength());
        Assert.Equal(3, hit.GetProperty("context")[0].GetProperty("chunkIndex").GetInt32());
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

    [Fact]
    public async Task Invoke_GetChunks_ReturnsChunks()
    {
        var search = new Mock<IWikiSearchService>();
        search.Setup(x => x.GetDocumentChunksAsync(It.IsAny<IReadOnlyCollection<long>>(), 5, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WikiDocumentChunkQueryResult
            {
                DocumentId = 5,
                DocumentName = "产品手册",
                Chunks = new List<WikiChunkContent>
                {
                    new() { ChunkId = 41, ChunkIndex = 2, Content = "第 2 片" },
                    new() { ChunkId = 42, ChunkIndex = 3, Content = "第 3 片" },
                },
            });

        var provider = new WikiAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);
        var tool = tools.Single(x => x.Name == WikiAppToolProvider.ChunkToolName);

        var result = await tool.InvokeAsync("{\"documentId\":5,\"chunkIndexes\":[2,3]}", CancellationToken.None);

        Assert.True(result.Success);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal(5, doc.RootElement.GetProperty("documentId").GetInt32());
        Assert.Equal("产品手册", doc.RootElement.GetProperty("document").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("chunks")[0].GetProperty("chunkIndex").GetInt32());
    }

    [Fact]
    public async Task Invoke_GetChunks_MissingOrInvalidArgs_Fails()
    {
        var provider = new WikiAppToolProvider(Mock.Of<IWikiSearchService>());
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);
        var tool = tools.Single(x => x.Name == WikiAppToolProvider.ChunkToolName);

        var missing = await tool.InvokeAsync("{}", CancellationToken.None);
        var negative = await tool.InvokeAsync("{\"documentId\":5,\"chunkIndexes\":[-1]}", CancellationToken.None);
        var tooMany = await tool.InvokeAsync("{\"documentId\":5,\"chunkIndexes\":[0,1,2,3,4,5,6,7,8,9,10]}", CancellationToken.None);
        var badJson = await tool.InvokeAsync("not-json", CancellationToken.None);

        Assert.False(missing.Success);
        Assert.False(negative.Success);
        Assert.False(tooMany.Success);
        Assert.False(badJson.Success);
    }

    [Fact]
    public async Task Invoke_GetChunks_DocumentNotInBoundWikis_FailsWithMessage()
    {
        var search = new Mock<IWikiSearchService>();
        search.Setup(x => x.GetDocumentChunksAsync(It.IsAny<IReadOnlyCollection<long>>(), 5, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MoAI.Infra.Exceptions.BusinessException("文档不存在或不属于绑定的知识库.") { StatusCode = 404 });

        var provider = new WikiAppToolProvider(search.Object);
        var tools = await provider.GetToolsAsync(Context(7), CancellationToken.None);
        var tool = tools.Single(x => x.Name == WikiAppToolProvider.ChunkToolName);

        var result = await tool.InvokeAsync("{\"documentId\":5,\"chunkIndexes\":[1]}", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("不属于", result.Error, System.StringComparison.Ordinal);
    }
}
