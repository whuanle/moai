using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using MoAI.Database.Entities;
using MoAI.Skill.Services;
using Moq;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 技能工具来源：注册门禁、沙箱写入与说明注入.
/// </summary>
public class SkillAppToolProviderTests
{
    private static readonly Guid SkillId = Guid.NewGuid();

    private static SkillRuntimeInfo Skill(params string[] paths) => new()
    {
        Id = SkillId,
        Key = "docx_writer",
        Name = "Word 文档生成",
        Description = "生成 docx.",
        Instructions = "# 说明\n步骤……",
        Files = paths.Select(p => new SkillRuntimeFile { Path = p, FileId = 0, ResourceName = $"MoAI.Skill.Resources.skills.docx_writer.{p}" }).ToList(),
    };

    private static AppAgentBuildContext Context(string executionSettings, IReadOnlyList<Guid>? skillIds = null) => new()
    {
        App = new AppEntity(),
        AppId = Guid.NewGuid(),
        TeamId = 1,
        SessionId = Guid.NewGuid(),
        Config = new AppAgentConfigEntity { ExecutionSettings = executionSettings },
        SkillIds = skillIds ?? [SkillId],
    };

    [Fact]
    public async Task GetTools_NoSkillBound_ReturnsEmpty()
    {
        var skillService = new Mock<ISkillService>();
        var provider = new SkillAppToolProvider(skillService.Object, Mock.Of<IAppSandboxService>());

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}", []), CancellationToken.None);

        Assert.Empty(tools);
        skillService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTools_SkillMissingOrDisabled_ReturnsEmpty()
    {
        var skillService = new Mock<ISkillService>();
        skillService
            .Setup(x => x.GetRuntimeSkillsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkillRuntimeInfo>());
        var provider = new SkillAppToolProvider(skillService.Object, Mock.Of<IAppSandboxService>());

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task GetTools_WithSkill_RegistersSkillTool()
    {
        var skillService = new Mock<ISkillService>();
        skillService
            .Setup(x => x.GetRuntimeSkillsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Skill("generate_docx.py") });
        var provider = new SkillAppToolProvider(skillService.Object, Mock.Of<IAppSandboxService>());

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal("skill_docx_writer", tool.Name);
        Assert.Equal("skill", tool.Kind);
        Assert.Contains("生成 docx", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_SandboxDisabled_ReturnsInstructionsOnly()
    {
        var skill = Skill("generate_docx.py");
        var skillService = new Mock<ISkillService>();
        skillService
            .Setup(x => x.GetRuntimeSkillsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { skill });
        var sandbox = new Mock<IAppSandboxService>();
        var provider = new SkillAppToolProvider(skillService.Object, sandbox.Object);

        var tools = await provider.GetToolsAsync(Context("{}"), CancellationToken.None);
        var result = await tools[0].InvokeAsync("{}", CancellationToken.None);

        Assert.True(result.Success);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal(skill.Instructions, doc.RootElement.GetProperty("instructions").GetString());
        Assert.False(doc.RootElement.GetProperty("sandboxEnabled").GetBoolean());
        Assert.Empty(doc.RootElement.GetProperty("files").EnumerateArray());
        sandbox.Verify(x => x.WriteFileAsync(It.IsAny<SandboxSessionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_SandboxEnabled_WritesFilesAndReturnsInstructions()
    {
        var skill = Skill("generate_docx.py");
        var skillService = new Mock<ISkillService>();
        skillService
            .Setup(x => x.GetRuntimeSkillsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { skill });
        skillService
            .Setup(x => x.ReadSkillFileAsync(It.IsAny<SkillRuntimeFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("print('skill')");
        var sandbox = new Mock<IAppSandboxService>();
        sandbox
            .Setup(x => x.WriteFileAsync(It.IsAny<SandboxSessionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"success\":true}");
        var provider = new SkillAppToolProvider(skillService.Object, sandbox.Object);

        var tools = await provider.GetToolsAsync(Context("{\"sandbox\":{\"enabled\":true}}"), CancellationToken.None);
        var result = await tools[0].InvokeAsync("{}", CancellationToken.None);

        Assert.True(result.Success);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal(skill.Instructions, doc.RootElement.GetProperty("instructions").GetString());
        Assert.True(doc.RootElement.GetProperty("sandboxEnabled").GetBoolean());
        Assert.Equal("/workspace/skills/docx_writer/generate_docx.py", doc.RootElement.GetProperty("files")[0].GetString());
        sandbox.Verify(
            x => x.WriteFileAsync(It.IsAny<SandboxSessionContext>(), It.Is<string>(p => p == "/workspace/skills/docx_writer/generate_docx.py"), It.Is<string>(c => c == "print('skill')"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
