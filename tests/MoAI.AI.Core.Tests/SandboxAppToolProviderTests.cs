using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using MoAI.Database.Entities;
using Moq;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 沙箱工具来源：启用门禁与工具清单.
/// </summary>
public class SandboxAppToolProviderTests
{
    private static AppAgentBuildContext Context(string executionSettings) => new()
    {
        App = new AppEntity(),
        AppId = Guid.NewGuid(),
        TeamId = 1,
        SessionId = Guid.NewGuid(),
        Config = new AppAgentConfigEntity { ExecutionSettings = executionSettings },
    };

    private static SandboxAppToolProvider CreateProvider() => new(Mock.Of<IAppSandboxService>());

    [Fact]
    public async Task GetTools_Disabled_ReturnsEmpty()
    {
        var provider = CreateProvider();

        var tools = await provider.GetToolsAsync(Context("{}"), CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task GetTools_ExplicitlyDisabled_ReturnsEmpty()
    {
        var provider = CreateProvider();

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":false}}"), CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task GetTools_Enabled_ReturnsSandboxTools()
    {
        var provider = CreateProvider();

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);

        var names = tools.Select(x => x.Name).ToList();
        Assert.Contains("sandbox_run_code", names);
        Assert.Contains("sandbox_run_shell", names);
        Assert.Contains("sandbox_write_file", names);
        Assert.Contains("sandbox_read_file", names);
        Assert.Contains("sandbox_list_dir", names);
        Assert.Contains("sandbox_delete_file", names);
        Assert.Contains("sandbox_search_files", names);
        Assert.All(tools, t => Assert.Equal("sandbox", t.Kind));
    }

    [Fact]
    public async Task InvokeRunCode_MissingCode_Fails()
    {
        var provider = CreateProvider();
        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);
        var runCode = tools.First(x => x.Name == "sandbox_run_code");

        var result = await runCode.InvokeAsync("{}", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("code", result.Error, StringComparison.Ordinal);
    }
}
