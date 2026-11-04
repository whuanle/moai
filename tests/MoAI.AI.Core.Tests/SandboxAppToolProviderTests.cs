using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using MoAI.Database.Entities;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using MoAI.Storage.Services;
using Moq;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 沙箱工具来源：启用门禁、工具清单与命令过滤拦截.
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

    private static (SandboxAppToolProvider Provider, Mock<IAppSandboxService> Sandbox) CreateProvider(SandboxLimitsSettings? limits = null)
    {
        var settingsService = new Mock<ISandboxSettingsService>();
        settingsService
            .Setup(x => x.GetLimitsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(limits ?? new SandboxLimitsSettings());
        var sandbox = new Mock<IAppSandboxService>();
        sandbox
            .Setup(x => x.RunShellAsync(It.IsAny<SandboxSessionContext>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"success\":true}");
        sandbox
            .Setup(x => x.RunCodeAsync(It.IsAny<SandboxSessionContext>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"success\":true}");
        return (new SandboxAppToolProvider(sandbox.Object, Mock.Of<IStorageService>(), settingsService.Object), sandbox);
    }

    [Fact]
    public async Task GetTools_Disabled_ReturnsEmpty()
    {
        var (provider, _) = CreateProvider();

        var tools = await provider.GetToolsAsync(Context("{}"), CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task GetTools_ExplicitlyDisabled_ReturnsEmpty()
    {
        var (provider, _) = CreateProvider();

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":false}}"), CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task GetTools_Enabled_ReturnsSandboxTools()
    {
        var (provider, _) = CreateProvider();

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);

        var names = tools.Select(x => x.Name).ToList();
        Assert.Contains("sandbox_run_code", names);
        Assert.Contains("sandbox_run_shell", names);
        Assert.Contains("sandbox_write_file", names);
        Assert.Contains("sandbox_read_file", names);
        Assert.Contains("sandbox_list_dir", names);
        Assert.Contains("sandbox_delete_file", names);
        Assert.Contains("sandbox_search_files", names);
        Assert.Contains("sandbox_save_artifact", names);
        Assert.All(tools, t => Assert.Equal("sandbox", t.Kind));
    }

    [Fact]
    public async Task InvokeRunCode_MissingCode_Fails()
    {
        var (provider, _) = CreateProvider();
        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);
        var runCode = tools.First(x => x.Name == "sandbox_run_code");

        var result = await runCode.InvokeAsync("{}", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("code", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeRunShell_AppFilterEnabled_BlocksDeniedCommand()
    {
        var (provider, sandbox) = CreateProvider();
        var tools = await provider.GetToolsAsync(
            Context("{\"sandbox\":{\"enabled\":true,\"commandFilter\":{\"enabled\":true,\"deny\":[\"rm\"]}}}"),
            CancellationToken.None);
        var runShell = tools.First(x => x.Name == "sandbox_run_shell");

        var result = await runShell.InvokeAsync("{\"command\":\"rm -rf /tmp/x\"}", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("拦截", result.Error, StringComparison.Ordinal);
        sandbox.Verify(
            x => x.RunShellAsync(It.IsAny<SandboxSessionContext>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeRunShell_AppFilterDisabled_OnlyPlatformListApplies()
    {
        var limits = new SandboxLimitsSettings { DeniedCommands = ["shutdown"] };
        var (provider, sandbox) = CreateProvider(limits);
        var tools = await provider.GetToolsAsync(
            Context("{\"sandbox\":{\"enabled\":true,\"commandFilter\":{\"enabled\":false,\"deny\":[\"rm\"]}}}"),
            CancellationToken.None);
        var runShell = tools.First(x => x.Name == "sandbox_run_shell");

        // 应用级未启用：rm 不拦（仅平台清单生效）
        var allowed = await runShell.InvokeAsync("{\"command\":\"rm -rf /tmp/x\"}", CancellationToken.None);
        Assert.True(allowed.Success);

        // 平台级清单强制：shutdown 拦截
        var blocked = await runShell.InvokeAsync("{\"command\":\"sudo shutdown now\"}", CancellationToken.None);
        Assert.False(blocked.Success);
        Assert.Contains("shutdown", blocked.Error, StringComparison.Ordinal);
        sandbox.Verify(
            x => x.RunShellAsync(It.IsAny<SandboxSessionContext>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeRunCode_BlocksDeniedPatternInCode()
    {
        var (provider, sandbox) = CreateProvider(new SandboxLimitsSettings { DeniedCommands = ["mkfs"] });
        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);
        var runCode = tools.First(x => x.Name == "sandbox_run_code");

        var result = await runCode.InvokeAsync(
            "{\"code\":\"import os\\nos.system('mkfs.ext4 /dev/sda1')\"}",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("拦截", result.Error, StringComparison.Ordinal);
        sandbox.Verify(
            x => x.RunCodeAsync(It.IsAny<SandboxSessionContext>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeRunShell_AllowedCommand_Executes()
    {
        var (provider, sandbox) = CreateProvider(new SandboxLimitsSettings { DeniedCommands = ["shutdown"] });
        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);
        var runShell = tools.First(x => x.Name == "sandbox_run_shell");

        var result = await runShell.InvokeAsync("{\"command\":\"ls -la\",\"cwd\":\"/workspace\"}", CancellationToken.None);

        Assert.True(result.Success);
        sandbox.Verify(
            x => x.RunShellAsync(It.IsAny<SandboxSessionContext>(), "ls -la", "/workspace", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
