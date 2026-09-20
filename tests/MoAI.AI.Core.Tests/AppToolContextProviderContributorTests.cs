using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using MoAI.Database.Entities;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// AppToolContextProviderContributor 的工具聚合与去重.
/// </summary>
public class AppToolContextProviderContributorTests
{
    private sealed class FakeProvider : IAppToolProvider
    {
        private readonly IReadOnlyList<AppTool> _tools;

        public FakeProvider(int order, params AppTool[] tools)
        {
            Order = order;
            _tools = tools;
        }

        public int Order { get; }

        public Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
            => Task.FromResult(_tools);
    }

    private static AppTool Tool(string name) => new()
    {
        Name = name,
        Title = name,
        Description = name,
        Kind = "static",
        InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{}")),
    };

    private static AppAgentBuildContext Context() => new() { App = new AppEntity(), AppId = System.Guid.NewGuid(), TeamId = 1 };

    // 审批模式下的闸口装配在独立用例中验证；此处传 null 走自动模式路径
    private static AppToolContextProviderContributor Contributor(params IAppToolProvider[] providers)
        => new(providers, approvalService: null!);

    [Fact]
    public async Task Create_NoTools_ReturnsNull()
    {
        var contributor = Contributor(new FakeProvider(10));

        var provider = await contributor.CreateAsync(Context(), CancellationToken.None);

        Assert.Null(provider);
    }

    [Fact]
    public async Task Create_AggregatesAndDeduplicatesByName()
    {
        var contributor = Contributor(
        [
            new FakeProvider(20, Tool("weather")),
            new FakeProvider(10, Tool("echo"), Tool("weather")),
        ]);

        var provider = await contributor.CreateAsync(Context(), CancellationToken.None);

        Assert.NotNull(provider);
        var casted = Assert.IsType<AppToolContextProvider>(provider);
        var json = await casted.BuildListJsonAsync(null);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
    }
}
