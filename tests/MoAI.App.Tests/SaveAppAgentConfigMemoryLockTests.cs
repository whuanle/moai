using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Commands;
using MoAI.App.Handlers;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Services;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// <see cref="SaveAppAgentConfigCommandHandler"/> 长期记忆向量化模型锁定：
/// 写入第一条记忆后模型/维度不可更换（参照系为生效配置，发布快照优先），需先清空应用全部记忆；
/// 关闭再开启同模型放行、落库 JSON 补齐生效模型/维度.
/// </summary>
public class SaveAppAgentConfigMemoryLockTests
{
    private static readonly Guid ModelA = Guid.CreateVersion7();
    private static readonly Guid ModelB = Guid.CreateVersion7();

    [Fact]
    public async Task Handle_ModelChangeWithExistingMemories_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedPublishedMemoryApp(db.Context, publishStatus: 1);
        SeedShard(db.Context, appId, memoryCount: 2);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, MemoryExecution(ModelB, 1024)),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("清空应用全部记忆", ex.Message, StringComparison.Ordinal);
        AssertConfigUnchanged(db.Context, appId);
    }

    [Fact]
    public async Task Handle_DimensionChangeWithExistingMemories_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedPublishedMemoryApp(db.Context, publishStatus: 1);
        SeedShard(db.Context, appId, memoryCount: 1);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, MemoryExecution(ModelA, 768)),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        AssertConfigUnchanged(db.Context, appId);
    }

    [Fact]
    public async Task Handle_DisabledDraftSwitchModelWithMemories_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedPublishedMemoryApp(db.Context, publishStatus: 1, draftMemoryEnabled: false);
        SeedShard(db.Context, appId, memoryCount: 2);
        var sut = CreateSut(db.Context);

        // 草稿此前处于关闭态，参照系仍是已发布生效配置：换模型同样拒绝（不会被关闭态绕过）
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, MemoryExecution(ModelB, 1024)),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        AssertConfigUnchanged(db.Context, appId);
    }

    [Fact]
    public async Task Handle_ReEnableWithSameModelAfterDisabledDraft_Succeeds()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedPublishedMemoryApp(db.Context, publishStatus: 1, draftMemoryEnabled: false);
        SeedShard(db.Context, appId, memoryCount: 2);
        SeedEmbeddingModel(db.Context, ModelA);
        var sut = CreateSut(db.Context);

        await sut.Handle(CreateCommand(appId, MemoryExecution(ModelA, 1024)), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Contains(ModelA.ToString(), config.ExecutionSettings, StringComparison.Ordinal);
        Assert.Contains("embeddingDimensions", config.ExecutionSettings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_RequestWithoutDimensions_InheritsEffectiveDimensions()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedPublishedMemoryApp(db.Context, publishStatus: 0);
        SeedEmbeddingModel(db.Context, ModelA);
        var sut = CreateSut(db.Context);

        // 请求开启记忆但不带维度：沿用生效配置的 1024 补齐落库，避免运行时回落默认值与既有向量不一致
        var element = JsonSerializer.SerializeToElement(new { memory = new { enabled = true, embeddingModelId = ModelA.ToString() } });
        await sut.Handle(CreateCommand(appId, element), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Contains("embeddingDimensions", config.ExecutionSettings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_NoMemories_ModelChangeAllowed()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedPublishedMemoryApp(db.Context, publishStatus: 0);
        SeedEmbeddingModel(db.Context, ModelB);
        var sut = CreateSut(db.Context);

        await sut.Handle(CreateCommand(appId, MemoryExecution(ModelB, 1024)), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Contains(ModelB.ToString(), config.ExecutionSettings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_RequestWithoutMemoryNodeWithMemories_PreservesEffectiveReference()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedPublishedMemoryApp(db.Context, publishStatus: 1);
        SeedShard(db.Context, appId, memoryCount: 3);
        var sut = CreateSut(db.Context);

        // 保存其他字段但未携带 memory 节：保留生效模型/维度作为锁定参照系（enabled=false 不改变运行时判定）
        var element = JsonSerializer.SerializeToElement(new { sandbox = new { enabled = false } });
        await sut.Handle(CreateCommand(appId, element), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Contains(ModelA.ToString(), config.ExecutionSettings, StringComparison.Ordinal);
        Assert.Contains("embeddingDimensions", config.ExecutionSettings, StringComparison.Ordinal);
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

    private static JsonElement MemoryExecution(Guid embedModelId, int dimensions)
        => JsonSerializer.SerializeToElement(new { memory = new { enabled = true, embeddingModelId = embedModelId.ToString(), embeddingDimensions = dimensions } });

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
    /// 建应用 + 已发布记忆配置（快照与草稿均为 memory enabled ModelA/1024；draftMemoryEnabled=false 时草稿仅 {enabled:false}）.
    /// </summary>
    private static Guid SeedPublishedMemoryApp(Database.DatabaseContext context, int publishStatus, bool draftMemoryEnabled = true)
    {
        var app = new AppEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "记忆锁定应用",
            Description = string.Empty,
            TeamId = 7,
            AppType = (int)AppType.Agent,
            Avatar = string.Empty,
            PublishStatus = (short)publishStatus,
        };
        context.Apps.Add(app);

        var enabledJson = JsonSerializer.Serialize(new { memory = new { enabled = true, embeddingModelId = ModelA.ToString(), embeddingDimensions = 1024 } });
        var draftJson = draftMemoryEnabled
            ? enabledJson
            : JsonSerializer.Serialize(new { memory = new { enabled = false } });
        var config = new AppAgentConfigEntity
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
            Skills = "[]",
            QuickInputs = "[]",
            ExecutionSettings = draftJson,
        };
        if (publishStatus == 1)
        {
            // 快照按 enabled 配置序列化：草稿关闭时生效配置仍指向已发布的 ModelA/1024
            var snapshotSource = new AppAgentConfigEntity { ExecutionSettings = enabledJson };
            config.PublishedConfig = AppAgentConfigSnapshot.Serialize(snapshotSource);
        }

        context.AppAgentConfigs.Add(config);
        context.SaveChanges();
        return app.Id;
    }

    private static void SeedShard(Database.DatabaseContext context, Guid appId, int memoryCount)
    {
        context.AppMemoryShards.Add(new AppMemoryShardEntity
        {
            Id = Guid.CreateVersion7(),
            AppId = appId,
            TeamId = 7,
            UserId = 1,
            MemoryCount = memoryCount,
            LastActiveTime = DateTimeOffset.Now,
        });
        context.SaveChanges();
    }

    private static void SeedEmbeddingModel(Database.DatabaseContext context, Guid modelId)
    {
        var channel = new AiChannelEntity
        {
            Id = Guid.NewGuid(),
            ProviderKey = "openai",
            Name = "embed渠道",
            ProtocolFamily = 1,
            BaseUrl = "http://127.0.0.1:9/v1",
            ApiKey = "sk-test",
            Enabled = true,
        };
        context.AiChannels.Add(channel);
        context.AiModels.Add(new AiModelEntity
        {
            Id = modelId,
            ChannelId = channel.Id,
            ModelId = "text-embedding-stub",
            Name = "桩向量化模型",
            ModelKind = "embedding",
            Enabled = true,
            IsPublic = true,
        });
        context.SaveChanges();
    }

    private static void AssertConfigUnchanged(Database.DatabaseContext context, Guid appId)
    {
        var config = context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.DoesNotContain(ModelB.ToString(), config.ExecutionSettings, StringComparison.Ordinal);
    }
}
