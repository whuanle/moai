using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Commands;
using MoAI.App.Handlers;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.Settings.Services;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// <see cref="SaveAppAgentConfigCommandHandler"/> 对知识图谱绑定（GraphIds）的校验与落库：
/// 空列表通过、非本团队/接入图 400、合法托管图通过（create/update 分支）.
/// </summary>
public class SaveAppAgentConfigCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithoutGraphIds_CreatesConfigWithEmptyGraphIds()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var modelId = SeedModel(db.Context);
        var sut = CreateSut(db.Context);

        await sut.Handle(CreateCommand(appId, modelId: modelId), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Equal("[]", config.GraphIds);
        Assert.Equal("[]", config.WikiIds);
    }

    [Fact]
    public async Task Handle_WhenGraphNotInTeam_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var modelId = SeedModel(db.Context);
        var foreignGraphId = SeedGraph(db.Context, teamId: 999, KnowledgeGraphModes.Managed);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, new[] { foreignGraphId }, modelId),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("知识图谱", ex.Message, StringComparison.Ordinal);
        Assert.Empty(db.Context.AppAgentConfigs);
    }

    [Fact]
    public async Task Handle_WhenGraphIsConnected_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var modelId = SeedModel(db.Context);
        var connectedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Connected);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, new[] { connectedGraphId }, modelId),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.Context.AppAgentConfigs);
    }

    [Fact]
    public async Task Handle_WhenTeamManagedGraph_SavesAndUpdatesGraphIds()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var modelId = SeedModel(db.Context);
        var managedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Managed);
        var anotherManagedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Managed);
        var connectedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Connected);
        var sut = CreateSut(db.Context);

        // create 分支：合法托管图落库，接入图/他团队图混入即整体拒绝
        await sut.Handle(CreateCommand(appId, new[] { managedGraphId }, modelId), CancellationToken.None);
        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Equal($"[{managedGraphId}]", config.GraphIds);

        // 混入接入图：整体拒绝且原配置不被破坏
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, new[] { managedGraphId, connectedGraphId }, modelId),
            CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal($"[{managedGraphId}]", config.GraphIds);

        // update 分支：替换为另一张托管图 + 去重
        await sut.Handle(CreateCommand(appId, new[] { anotherManagedGraphId, anotherManagedGraphId }, modelId), CancellationToken.None);
        Assert.Equal($"[{anotherManagedGraphId}]", config.GraphIds);
    }

    private static SaveAppAgentConfigCommand CreateCommand(Guid appId, long[]? graphIds = null, Guid? modelId = null, string? prompt = null)
        => new()
        {
            AppId = appId,
            ContextUserId = 1,
            ModelId = modelId,
            Prompt = prompt ?? "你是测试助手",
            WikiIds = Array.Empty<long>(),
            GraphIds = graphIds ?? Array.Empty<long>(),
            Plugins = Array.Empty<Guid>(),
        };

    [Fact]
    public async Task Handle_WhenModelIdMissing_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, modelId: null),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("对话模型", ex.Message, StringComparison.Ordinal);
        Assert.Empty(db.Context.AppAgentConfigs);
    }

    [Fact]
    public async Task Handle_WhenPromptEmpty_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var modelId = SeedModel(db.Context);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, modelId: modelId, prompt: "  "),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("系统提示词", ex.Message, StringComparison.Ordinal);
        Assert.Empty(db.Context.AppAgentConfigs);
    }

    /// <summary>
    /// 造一个本团队自有渠道 + 启用中的对话模型（对话模型必填校验后，合法保存须携带 DB 中存在的可用模型）；
    /// 渠道仅落库不发起请求，密钥值随机生成、无真实凭据语义.
    /// </summary>
    private static Guid SeedModel(Database.DatabaseContext context)
    {
        var channel = new AiChannelEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = 7,
            ProviderKey = "openai",
            Name = $"渠道-{Guid.NewGuid():N}",
            ProtocolFamily = 0,
            BaseUrl = "http://127.0.0.1:9/v1",
            ApiKey = $"test-{Guid.NewGuid():N}",
            Enabled = true,
        };
        context.AiChannels.Add(channel);

        var model = new AiModelEntity
        {
            Id = Guid.CreateVersion7(),
            ChannelId = channel.Id,
            ModelId = $"test-chat-{Guid.NewGuid():N}",
            Name = "测试对话模型",
            ModelKind = "conversation",
            Enabled = true,
        };
        context.AiModels.Add(model);
        context.SaveChanges();
        return model.Id;
    }

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

    private static Guid SeedApp(Database.DatabaseContext context)
    {
        var app = new AppEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "助手",
            Description = string.Empty,
            TeamId = 7,
            AppType = (int)AppType.Agent,
            Avatar = string.Empty,
        };
        context.Apps.Add(app);
        context.SaveChanges();
        return app.Id;
    }

    private static long SeedGraph(Database.DatabaseContext context, int teamId, string mode)
    {
        var graph = new KnowledgeGraphEntity
        {
            TeamId = teamId,
            Name = $"图-{Guid.NewGuid():N}",
            Description = string.Empty,
            Mode = mode,
            AvatarPath = string.Empty,
        };
        context.KnowledgeGraphs.Add(graph);
        context.SaveChanges();
        return graph.Id;
    }
}
