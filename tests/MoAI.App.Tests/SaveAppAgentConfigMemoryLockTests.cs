using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Commands;
using MoAI.App.Handlers;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Services;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// <see cref="SaveAppAgentConfigCommandHandler"/> 长期记忆配置统一口径：
/// 向量化模型/维度由系统设置（MEMORY_*）统一决定，应用配置不接收这两个字段（携带时剥离）；
/// 平台未开启长期记忆（总开关关或未选模型）时应用开启记忆被拒绝.
/// </summary>
public class SaveAppAgentConfigMemoryLockTests
{
    private static readonly Guid ModelA = Guid.CreateVersion7();

    [Fact]
    public async Task Handle_MemoryEnabled_PlatformOff_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, MemoryExecution(enabled: true)),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("平台未开启长期记忆", ex.Message, StringComparison.Ordinal);
        AssertConfigMemoryAbsent(db.Context, appId);
    }

    [Fact]
    public async Task Handle_MemoryEnabled_PlatformOnWithoutModel_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        SeedSetting(db.Context, SettingDefinitions.MemoryEnabledKey, "true");
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, MemoryExecution(enabled: true)),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        AssertConfigMemoryAbsent(db.Context, appId);
    }

    [Fact]
    public async Task Handle_MemoryEnabled_PlatformOn_Succeeds_AndStripsModelFields()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        SeedSetting(db.Context, SettingDefinitions.MemoryEnabledKey, "true");
        SeedSetting(db.Context, SettingDefinitions.MemoryEmbeddingModelKey, ModelA.ToString());
        var sut = CreateSut(db.Context);

        // 请求携带模型/维度：统一在系统设置，落库时剥离
        await sut.Handle(CreateCommand(appId, MemoryExecution(enabled: true)), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Contains("\"enabled\":true", config.ExecutionSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("embeddingModelId", config.ExecutionSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("embeddingDimensions", config.ExecutionSettings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_MemoryDisabled_PlatformOff_Succeeds()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var sut = CreateSut(db.Context);

        // 关闭记忆不依赖平台开关：历史存量应用的关闭态保存不受影响
        await sut.Handle(CreateCommand(appId, MemoryExecution(enabled: false)), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Contains("\"enabled\":false", config.ExecutionSettings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_NoMemoryNode_PlatformOff_Succeeds()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var sut = CreateSut(db.Context);

        var element = JsonSerializer.SerializeToElement(new { sandbox = new { enabled = false } });
        await sut.Handle(CreateCommand(appId, element), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Contains("sandbox", config.ExecutionSettings, StringComparison.Ordinal);
    }

    private static SaveAppAgentConfigCommand CreateCommand(Guid appId, JsonElement? executionSettings = null)
        => new()
        {
            AppId = appId,
            ContextUserId = 1,
            ModelId = null,
            WikiIds = Array.Empty<long>(),
            GraphIds = Array.Empty<long>(),
            Plugins = Array.Empty<Guid>(),
            ExecutionSettings = executionSettings,
        };

    private static JsonElement MemoryExecution(bool enabled)
        => JsonSerializer.SerializeToElement(new
        {
            memory = new
            {
                enabled,
                embeddingModelId = ModelA.ToString(),
                embeddingDimensions = 1024,
            },
        });

    private static SaveAppAgentConfigCommandHandler CreateSut(Database.DatabaseContext context)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);

        return new SaveAppAgentConfigCommandHandler(
            context,
            teamService.Object,
            Mock.Of<ISandboxSettingsService>());
    }

    /// <summary>
    /// 建应用 + 空执行参数配置行.
    /// </summary>
    private static Guid SeedApp(Database.DatabaseContext context)
    {
        var app = new AppEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "记忆统一口径应用",
            Description = string.Empty,
            TeamId = 7,
            AppType = (int)AppType.Agent,
            Avatar = string.Empty,
            PublishStatus = 0,
        };
        context.Apps.Add(app);

        context.AppAgentConfigs.Add(new AppAgentConfigEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = app.TeamId,
            AppId = app.Id,
            Prompt = string.Empty,
            OpeningStatement = string.Empty,
            WikiIds = "[]",
            GraphIds = "[]",
            Plugins = "[]",
            WorkflowApps = "[]",
            AgentApps = "[]",
            Skills = "[]",
            QuickInputs = "[]",
            ExecutionSettings = "{}",
        });
        context.SaveChanges();
        return app.Id;
    }

    private static void SeedSetting(Database.DatabaseContext context, string key, string value)
    {
        context.Settings.Add(new SettingEntity
        {
            Key = key,
            Name = key,
            Description = key,
            Value = value,
        });
        context.SaveChanges();
    }

    private static void AssertConfigMemoryAbsent(Database.DatabaseContext context, Guid appId)
    {
        var config = context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.DoesNotContain("memory", config.ExecutionSettings, StringComparison.Ordinal);
    }
}
