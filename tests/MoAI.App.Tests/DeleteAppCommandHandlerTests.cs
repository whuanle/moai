using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Controllers;
using MoAI.App.Handlers;
using MoAI.App.Queries;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

public class DeleteAppCommandHandlerTests
{
    [Theory]
    [InlineData(TeamRole.Owner)]
    [InlineData(TeamRole.Admin)]
    public async Task Controller_Manager_UsesAppTeamAndForwardsDelete(TeamRole role)
    {
        using var db = TestSqliteContext.Create();
        var app = SeedApp(db.Context);
        var teams = new Mock<ITeamService>(MockBehavior.Strict);
        teams.Setup(x => x.GetMyRoleAsync(7, 123, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<DeleteAppCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyCommandResponse.Default);
        var controller = CreateController(db.Context, teams.Object, mediator.Object);

        await controller.DeleteApp(app.Id, CancellationToken.None);

        mediator.Verify(x => x.Send(It.Is<DeleteAppCommand>(command => command.AppId == app.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(TeamRole.Member, false, 403)]
    [InlineData(TeamRole.Member, true, 403)]
    [InlineData(null, false, 404)]
    [InlineData(null, true, 404)]
    public async Task Controller_Unauthorized_DoesNotSendDelete(TeamRole? role, bool external, int status)
    {
        using var db = TestSqliteContext.Create();
        var app = SeedApp(db.Context, external: external);
        var teams = new Mock<ITeamService>();
        teams.Setup(x => x.GetMyRoleAsync(7, 123, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        var mediator = new Mock<IMediator>(MockBehavior.Strict);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateController(db.Context, teams.Object, mediator.Object).DeleteApp(app.Id, CancellationToken.None));

        Assert.Equal(status, exception.StatusCode);
        Assert.True(await db.Context.Apps.AnyAsync(x => x.Id == app.Id));
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_DeletesOnlyTargetAndBinding_PreservesHistoryAndSharedConnection()
    {
        using var db = TestSqliteContext.Create();
        var app = SeedApp(db.Context);
        var other = SeedApp(db.Context, "其他应用", teamId: 8);
        var feishuId = Guid.CreateVersion7();
        db.Context.FeishuApps.Add(new FeishuAppEntity
        {
            Id = feishuId, TeamId = 7, Name = "共享飞书", Description = string.Empty, AppId = "cli_test", AppSecret = "unused", Domain = "https://open.feishu.cn",
        });
        db.Context.FeishuAppBindings.AddRange(
            new FeishuAppBindingEntity { Id = 1, FeishuAppId = feishuId, ChannelType = (int)FeishuChannelType.App, ChannelId = app.Id.ToString() },
            new FeishuAppBindingEntity { Id = 2, FeishuAppId = feishuId, ChannelType = (int)FeishuChannelType.WikiSource, ChannelId = "source-id" });
        db.Context.DingTalkAppBindings.AddRange(
            new DingTalkAppBindingEntity { Id = 1, DingTalkAppId = Guid.CreateVersion7(), ChannelType = (int)DingTalkChannelType.App, ChannelId = app.Id.ToString() },
            new DingTalkAppBindingEntity { Id = 2, DingTalkAppId = Guid.CreateVersion7(), ChannelType = (int)DingTalkChannelType.App, ChannelId = other.Id.ToString() });
        db.Context.WeixinWorkAppBindings.AddRange(
            new WeixinWorkAppBindingEntity { Id = 1, WeixinWorkAppId = Guid.CreateVersion7(), ChannelType = (int)WeixinWorkChannelType.App, ChannelId = app.Id.ToString() },
            new WeixinWorkAppBindingEntity { Id = 2, WeixinWorkAppId = Guid.CreateVersion7(), ChannelType = (int)WeixinWorkChannelType.App, ChannelId = other.Id.ToString() });
        var sessionId = Guid.CreateVersion7();
        db.Context.AppAgentSessions.Add(new AppAgentSessionEntity { Id = sessionId, AppId = app.Id, TeamId = 7, Title = "历史" });
        await db.Context.SaveChangesAsync();

        await new DeleteAppCommandHandler(db.Context).Handle(new DeleteAppCommand { AppId = app.Id }, CancellationToken.None);

        Assert.False(await db.Context.Apps.AnyAsync(x => x.Id == app.Id));
        Assert.NotEqual(0, await db.Context.Apps.IgnoreQueryFilters().Where(x => x.Id == app.Id).Select(x => x.IsDeleted).SingleAsync());
        Assert.True(await db.Context.Apps.AnyAsync(x => x.Id == other.Id));
        Assert.Single(await db.Context.FeishuAppBindings.ToListAsync());
        Assert.Equal(other.Id.ToString(), (await db.Context.DingTalkAppBindings.SingleAsync()).ChannelId);
        Assert.Equal(other.Id.ToString(), (await db.Context.WeixinWorkAppBindings.SingleAsync()).ChannelId);
        Assert.Single(await db.Context.FeishuApps.ToListAsync());
        Assert.True(await db.Context.AppAgentSessions.AnyAsync(x => x.Id == sessionId));
        var repeated = await Assert.ThrowsAsync<BusinessException>(() => new DeleteAppCommandHandler(db.Context)
            .Handle(new DeleteAppCommand { AppId = app.Id }, CancellationToken.None));
        Assert.Equal(404, repeated.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_WorkflowReferencedByAgentDraftOrSnapshot_Throws409(bool published)
    {
        using var db = TestSqliteContext.Create();
        var target = SeedApp(db.Context, "流程");
        var owner = SeedApp(db.Context, "调用方");
        var config = Config(owner.Id);
        config.WorkflowApps = JsonSerializer.Serialize(new[] { target.Id });
        if (published)
        {
            config.PublishedConfig = AppAgentConfigSnapshot.Serialize(config);
            config.WorkflowApps = "[]";
        }

        db.Context.AppAgentConfigs.Add(config);
        await db.Context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessException>(() => new DeleteAppCommandHandler(db.Context)
            .Handle(new DeleteAppCommand { AppId = target.Id }, CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Contains("调用方", exception.Message);
        Assert.True(await db.Context.Apps.AnyAsync(x => x.Id == target.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_AgentReferencedByWorkflowDraftOrSnapshot_Throws409(bool published)
    {
        using var db = TestSqliteContext.Create();
        var target = SeedApp(db.Context);
        var owner = SeedApp(db.Context, "流程调用方");
        var definition = JsonSerializer.Serialize(new { nodes = new[] { new { type = "agentApp", config = new { agentAppId = target.Id } } } });
        db.Context.AppWorkflowConfigs.Add(new AppWorkflowConfigEntity
        {
            Id = Guid.CreateVersion7(), AppId = owner.Id, TeamId = 7, DraftEditorData = "{}",
            DraftDefinition = published ? "{}" : definition, PublishedDefinition = published ? definition : null,
        });
        await db.Context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessException>(() => new DeleteAppCommandHandler(db.Context)
            .Handle(new DeleteAppCommand { AppId = target.Id }, CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.True(await db.Context.Apps.AnyAsync(x => x.Id == target.Id));
    }

    [Fact]
    public async Task DeletedApp_DetailNewSessionAndExistingMessages_Return404_OtherAppStillWorks()
    {
        using var db = TestSqliteContext.Create();
        var target = SeedApp(db.Context);
        var other = SeedApp(db.Context, "仍可用");
        var teams = new Mock<ITeamService>();
        teams.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(TeamRole.Admin);
        var sessionId = Guid.CreateVersion7();
        db.Context.AppAgentSessions.Add(new AppAgentSessionEntity { Id = sessionId, AppId = target.Id, TeamId = 7, Title = "历史" });
        await db.Context.SaveChangesAsync();
        await new DeleteAppCommandHandler(db.Context).Handle(new DeleteAppCommand { AppId = target.Id }, CancellationToken.None);

        var detail = await Assert.ThrowsAsync<BusinessException>(() => new QueryAppCommandHandler(db.Context, teams.Object)
            .Handle(new QueryAppCommand { AppId = target.Id }, CancellationToken.None));
        var create = await Assert.ThrowsAsync<BusinessException>(() => new CreateAppSessionCommandHandler(db.Context, teams.Object, Mock.Of<IAppUsageCounter>())
            .Handle(new CreateAppSessionCommand { AppId = target.Id }, CancellationToken.None));
        var messages = await Assert.ThrowsAsync<BusinessException>(() => new QueryAppSessionMessagesCommandHandler(db.Context, new SystemOptions { Storage = new() })
            .Handle(new QueryAppSessionMessagesCommand { SessionId = sessionId }, CancellationToken.None));

        Assert.Equal(404, detail.StatusCode);
        Assert.Equal(404, create.StatusCode);
        Assert.Equal(404, messages.StatusCode);
        var stillAvailable = await new QueryAppCommandHandler(db.Context, teams.Object)
            .Handle(new QueryAppCommand { AppId = other.Id }, CancellationToken.None);
        Assert.Equal(other.Id, stillAvailable.AppId);
    }

    private static AppController CreateController(DatabaseContext db, ITeamService teams, IMediator mediator)
    {
        var user = new Mock<UserContext>();
        user.SetupGet(x => x.UserId).Returns(123);
        var provider = new Mock<IUserContextProvider>();
        provider.Setup(x => x.GetUserContext()).Returns(user.Object);
        return new AppController(mediator, provider.Object, db, teams);
    }

    private static AppEntity SeedApp(DatabaseContext db, string name = "目标应用", int teamId = 7, bool external = false)
    {
        var app = new AppEntity { Id = Guid.CreateVersion7(), Name = name, Description = string.Empty, TeamId = teamId, Avatar = string.Empty, IsExternal = external };
        db.Apps.Add(app);
        db.SaveChanges();
        return app;
    }

    private static AppAgentConfigEntity Config(Guid ownerId) => new()
    {
        Id = Guid.CreateVersion7(), AppId = ownerId, TeamId = 7, Prompt = string.Empty, WikiIds = "[]", Plugins = "[]", ExecutionSettings = "{}",
        Skills = "[]", OpeningStatement = string.Empty, Prompts = "[]", QuickInputs = "[]", WorkflowApps = "[]", GraphIds = "[]",
    };
}
