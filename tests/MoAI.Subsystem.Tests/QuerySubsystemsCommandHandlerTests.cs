using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Queries;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 子系统列表查询处理器测试：授权团队名称清单回显.
/// </summary>
public class QuerySubsystemsCommandHandlerTests
{
    private static QuerySubsystemsCommandHandler CreateHandler(DatabaseContext context)
    {
        // 松散 mock 的属性成员默认返回 null，Database 需显式给 IDatabase mock（KeyExistsAsync 默认 false=离线）
        var redis = new Mock<StackExchange.Redis.Extensions.Core.Abstractions.IRedisDatabase>();
        redis.SetupGet(x => x.Database).Returns(Mock.Of<StackExchange.Redis.IDatabase>());
        var onlineService = new SubsystemOnlineService(redis.Object);
        return new QuerySubsystemsCommandHandler(context, onlineService, Mock.Of<IUserInfoFillService>());
    }

    private static async Task<SubsystemEntity> SeedSubsystemAsync(DatabaseContext context, string key, SubsystemTeamScope scope, params int[] teamIds)
    {
        var entity = new SubsystemEntity
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            Name = key,
            Description = string.Empty,
            Secret = "hash",
            SecretSalt = "salt",
            Status = SubsystemStatus.Enabled,
            TeamScope = scope,
        };
        context.Subsystems.Add(entity);
        foreach (var teamId in teamIds)
        {
            context.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity { Id = Guid.CreateVersion7(), SubsystemId = entity.Id, TeamId = teamId });
        }

        await context.SaveChangesAsync();
        return entity;
    }

    [Fact]
    public async Task Handle_SpecifiedTeams_ReturnsAuthorizedTeamNames()
    {
        using var db = TestSqliteContext.Create();
        CreateSubsystemCommandHandlerTests.SeedTeam(db.Context, 1);
        CreateSubsystemCommandHandlerTests.SeedTeam(db.Context, 2);
        // 团队 99 不存在：授权行仍在（历史数据），名称应回退 #99
        await SeedSubsystemAsync(db.Context, "all", SubsystemTeamScope.AllTeams);
        await SeedSubsystemAsync(db.Context, "specified", SubsystemTeamScope.SpecifiedTeams, 2, 1, 99);

        var handler = CreateHandler(db.Context);
        var response = await handler.Handle(new QuerySubsystemsCommand(), CancellationToken.None);

        var specified = response.Items.Single(x => x.Key == "specified");
        Assert.Equal(3, specified.AuthorizedTeamCount);
        // 按团队 id 升序，名称取自团队表（种子命名为 team-{id}）；已删除团队回退 #id
        Assert.Equal(["team-1", "team-2", "#99"], specified.AuthorizedTeams.Select(x => x.TeamName).ToList());
        Assert.Equal([1, 2, 99], specified.AuthorizedTeams.Select(x => x.TeamId).ToList());

        var all = response.Items.Single(x => x.Key == "all");
        Assert.Empty(all.AuthorizedTeams);
        Assert.Equal(0, all.AuthorizedTeamCount);
    }
}
