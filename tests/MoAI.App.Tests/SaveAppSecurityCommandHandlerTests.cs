using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Commands;
using MoAI.App.Handlers;
using MoAI.App.Queries;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// 应用安全配置（内容脱敏）保存/查询：Admin 保存与回显、Member 403、非法规则 400、未配置默认值.
/// </summary>
public class SaveAppSecurityCommandHandlerTests
{
    [Fact]
    public async Task Handle_AdminSave_CreatesConfigAndQueryEchoesBack()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        await CreateSut(db.Context, TeamRole.Admin).Handle(new SaveAppSecurityCommand
        {
            AppId = appId,
            Enabled = true,
            MaskToolResult = true,
            MaskToolArgs = true,
            MaskModelOutput = false,
            Rules =
            [
                new AppSecurityPolicy.AppSecurityRule { Name = "手机号", Type = "phone" },
                new AppSecurityPolicy.AppSecurityRule { Name = "订单", Type = "custom", Pattern = @"ORD-\d{6}", Replacement = "[订单]" },
            ],
            ModelOutputRules =
            [
                new AppSecurityPolicy.AppSecurityRule { Name = "邮箱", Type = "email" },
            ],
        }, CancellationToken.None);

        var query = await CreateQuerySut(db.Context, TeamRole.Admin).Handle(new QueryAppSecurityCommand { AppId = appId, ContextUserId = 1 }, CancellationToken.None);
        Assert.True(query.Enabled);
        Assert.True(query.MaskToolResult);
        Assert.True(query.MaskToolArgs);
        Assert.False(query.MaskModelOutput);
        Assert.Equal(2, query.Rules.Count);
        Assert.Equal("phone", query.Rules[0].Type);
        Assert.Equal(@"ORD-\d{6}", query.Rules[1].Pattern);
        Assert.Equal("[订单]", query.Rules[1].Replacement);
        Assert.Single(query.ModelOutputRules);
        Assert.Equal("email", query.ModelOutputRules[0].Type);
    }

    [Fact]
    public async Task Handle_Member_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Member).Handle(
            new SaveAppSecurityCommand { AppId = appId, Enabled = true },
            CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
        Assert.Empty(db.Context.AppSecurityConfigs);
    }

    [Fact]
    public async Task Handle_CustomRuleWithInvalidRegex_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Admin).Handle(
            new SaveAppSecurityCommand
            {
                AppId = appId,
                Enabled = true,
                Rules = [new AppSecurityPolicy.AppSecurityRule { Name = "坏", Type = "custom", Pattern = "([" }],
            },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.Context.AppSecurityConfigs);
    }

    [Fact]
    public async Task Handle_UnknownRuleType_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Admin).Handle(
            new SaveAppSecurityCommand
            {
                AppId = appId,
                Enabled = true,
                Rules = [new AppSecurityPolicy.AppSecurityRule { Name = "坏", Type = "gps" }],
            },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Query_WithoutSavedConfig_ReturnsDefaults()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var query = await CreateQuerySut(db.Context, TeamRole.Member).Handle(new QueryAppSecurityCommand { AppId = appId, ContextUserId = 1 }, CancellationToken.None);

        Assert.False(query.Enabled);
        Assert.True(query.MaskToolResult);
        Assert.False(query.MaskToolArgs);
        Assert.False(query.MaskModelOutput);
        Assert.Empty(query.Rules);
        Assert.Empty(query.ModelOutputRules);
    }

    [Fact]
    public async Task Handle_SaveWithDisabled_KeepsBothRuleSets()
    {
        // 关闭总开关保存不清空已配置规则：两组规则随保存原样持久化（UI 仅隐藏编辑器）
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        await CreateSut(db.Context, TeamRole.Admin).Handle(new SaveAppSecurityCommand
        {
            AppId = appId,
            Enabled = false,
            MaskModelOutput = true,
            Rules = [new AppSecurityPolicy.AppSecurityRule { Name = "手机号", Type = "phone" }],
            ModelOutputRules = [new AppSecurityPolicy.AppSecurityRule { Name = "邮箱", Type = "email" }],
        }, CancellationToken.None);

        var query = await CreateQuerySut(db.Context, TeamRole.Admin).Handle(new QueryAppSecurityCommand { AppId = appId, ContextUserId = 1 }, CancellationToken.None);
        Assert.False(query.Enabled);
        Assert.Single(query.Rules);
        Assert.Single(query.ModelOutputRules);
    }

    private static SaveAppSecurityCommandHandler CreateSut(Database.DatabaseContext context, TeamRole role)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        return new SaveAppSecurityCommandHandler(context, teamService.Object);
    }

    private static QueryAppSecurityCommandHandler CreateQuerySut(Database.DatabaseContext context, TeamRole role)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        return new QueryAppSecurityCommandHandler(context, teamService.Object);
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
}
