using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Handlers;
using MoAI.App.Queries;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// 应用用量统计：会话数/去重用户数/消息数/最近使用时间与汇总、按模型分布.
/// 回归：原 GroupBy(1)+Distinct().Count() 被 EF 翻译为 COUNT(DISTINCT *) 非法 SQL（PostgreSQL 42601），已改为取回内存计算.
/// </summary>
public class QueryAppUsageCommandHandlerTests
{
    [Fact]
    public async Task Handle_AggregatesStatsSummaryAndByModel()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var now = DateTimeOffset.Now;

        // 两个模型各一条审计（同应用同团队），汇总与按模型分布均来自聚合用量表
        db.Context.AiModelTokenAudits.Add(new AiModelTokenAuditEntity
        {
            Id = 1, ModelId = Guid.CreateVersion7(), TeamId = 7, UseType = (int)AiModelUseType.App, UseResourceId = appId,
            Count = 2, PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15,
        });
        db.Context.AiModelTokenAudits.Add(new AiModelTokenAuditEntity
        {
            Id = 2, ModelId = Guid.CreateVersion7(), TeamId = 7, UseType = (int)AiModelUseType.App, UseResourceId = appId,
            Count = 1, PromptTokens = 20, CompletionTokens = 10, TotalTokens = 30,
        });

        // 四个会话、三个去重用户：去重键为 (CreateUserId, UserType)（审计注入会把 CreateUserId 统一为当前用户，
        // 用例以不同 UserType 构造不同对；重复对 s4 与 s1 不重复计数）；同应用的其他团队会话不计入
        var s1 = Guid.CreateVersion7();
        var s2 = Guid.CreateVersion7();
        var s3 = Guid.CreateVersion7();
        var s4 = Guid.CreateVersion7();
        SeedSession(db.Context, s1, appId, createUserId: 1, userType: 0, lastActive: now.AddMinutes(-3));
        SeedSession(db.Context, s2, appId, createUserId: 1, userType: 1, lastActive: now.AddMinutes(-1));
        SeedSession(db.Context, s3, appId, createUserId: 2, userType: 2, lastActive: now.AddMinutes(-2));
        SeedSession(db.Context, s4, appId, createUserId: 1, userType: 0, lastActive: now.AddMinutes(-4));

        db.Context.AppAgentMessages.Add(new AppAgentMessageEntity { Id = Guid.CreateVersion7(), SessionId = s1, Seq = 1, Role = "user", Content = "1", CompletionsId = string.Empty, ToolCalls = string.Empty, ToolCallId = string.Empty, Reasoning = string.Empty });
        db.Context.AppAgentMessages.Add(new AppAgentMessageEntity { Id = Guid.CreateVersion7(), SessionId = s1, Seq = 2, Role = "assistant", Content = "2", CompletionsId = string.Empty, ToolCalls = string.Empty, ToolCallId = string.Empty, Reasoning = string.Empty });
        db.Context.AppAgentMessages.Add(new AppAgentMessageEntity { Id = Guid.CreateVersion7(), SessionId = s2, Seq = 1, Role = "user", Content = "3", CompletionsId = string.Empty, ToolCalls = string.Empty, ToolCallId = string.Empty, Reasoning = string.Empty });
        db.Context.SaveChanges();

        var response = await CreateSut(db.Context, TeamRole.Admin).Handle(
            new QueryAppUsageCommand { AppId = appId, ContextUserId = 1 },
            CancellationToken.None);

        Assert.Equal(4, response.Stats.SessionCount);
        Assert.Equal(3, response.Stats.UserCount);
        Assert.Equal(3, response.Stats.MessageCount);
        Assert.Equal(now.AddMinutes(-1), response.Stats.LastActiveTime);

        Assert.Equal(3, response.Summary.CallCount);
        Assert.Equal(45, response.Summary.TotalTokens);
        Assert.Equal(2, response.ByModel.Count);
    }

    [Fact]
    public async Task Handle_NoSessions_StatsAreZero()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var response = await CreateSut(db.Context, TeamRole.Owner).Handle(
            new QueryAppUsageCommand { AppId = appId, ContextUserId = 1 },
            CancellationToken.None);

        Assert.Equal(0, response.Stats.SessionCount);
        Assert.Equal(0, response.Stats.UserCount);
        Assert.Equal(0, response.Stats.MessageCount);
        Assert.Null(response.Stats.LastActiveTime);
        Assert.Equal(0, response.Summary.TotalTokens);
        Assert.Empty(response.ByModel);
    }

    [Fact]
    public async Task Handle_Member_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Member).Handle(
            new QueryAppUsageCommand { AppId = appId, ContextUserId = 1 },
            CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
    }

    private static QueryAppUsageCommandHandler CreateSut(Database.DatabaseContext context, TeamRole role)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        return new QueryAppUsageCommandHandler(context, teamService.Object);
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

    private static void SeedSession(Database.DatabaseContext context, Guid id, Guid appId, long createUserId, int userType, DateTimeOffset lastActive)
    {
        context.AppAgentSessions.Add(new AppAgentSessionEntity
        {
            Id = id,
            TeamId = 7,
            AppId = appId,
            Title = "会话",
            UserType = userType,
            CreateUserId = createUserId,
            LastMessageTime = lastActive,
        });
    }
}
