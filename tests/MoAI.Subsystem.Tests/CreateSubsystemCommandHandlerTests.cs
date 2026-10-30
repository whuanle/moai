using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Handlers;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 创建子系统命令处理器测试.
/// </summary>
public class CreateSubsystemCommandHandlerTests
{
    [Fact]
    public async Task Handle_AllTeams_CreatesAndReturnsSecretOnce()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSubsystemCommandHandler(db.Context);

        var response = await handler.Handle(new CreateSubsystemCommand
        {
            Key = "report",
            Name = "智能报表",
            Description = "报表子系统",
            TeamScope = SubsystemTeamScope.AllTeams,
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.SubsystemId);
        Assert.Equal(48, response.Secret.Length);

        var entity = await db.Context.Subsystems.FindAsync(response.SubsystemId);
        Assert.NotNull(entity);
        Assert.Equal("report", entity!.Key);
        Assert.Equal(SubsystemStatus.Enabled, entity.Status);

        // 库内只存哈希，绝不落原文
        Assert.NotEqual(response.Secret, entity.Secret);
        Assert.False(string.IsNullOrEmpty(entity.SecretSalt));
        Assert.True(MoAI.Infra.Helpers.PBKDF2Helper.VerifyHash(response.Secret, entity.Secret, entity.SecretSalt));

        // 全员范围不写授权行
        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == entity.Id).ToList());
    }

    [Fact]
    public async Task Handle_SpecifiedTeams_WritesDistinctAuths()
    {
        using var db = TestSqliteContext.Create();
        SeedTeam(db.Context, 1);
        SeedTeam(db.Context, 2);
        var handler = new CreateSubsystemCommandHandler(db.Context);

        var response = await handler.Handle(new CreateSubsystemCommand
        {
            Key = "report",
            Name = "智能报表",
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [1, 2, 2],
        }, CancellationToken.None);

        var auths = db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == response.SubsystemId).ToList();
        Assert.Equal([1, 2], auths.Select(x => x.TeamId).OrderBy(x => x).ToList());
    }

    [Fact]
    public async Task Handle_DuplicateKey_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSubsystemCommandHandler(db.Context);
        await handler.Handle(new CreateSubsystemCommand { Key = "report", Name = "a", TeamScope = SubsystemTeamScope.AllTeams }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new CreateSubsystemCommand { Key = "report", Name = "b", TeamScope = SubsystemTeamScope.AllTeams }, CancellationToken.None));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_MissingTeam_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSubsystemCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new CreateSubsystemCommand
        {
            Key = "report",
            Name = "a",
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [998, 999],
        }, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("998", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SpecifiedTeamsWithoutTeamIds_Fails()
    {
        var validator = new InlineValidator<CreateSubsystemCommand>();
        CreateSubsystemCommand.Validate(validator);

        var result = validator.Validate(new CreateSubsystemCommand
        {
            Key = "report",
            Name = "a",
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [],
        });
        Assert.False(result.IsValid);
    }

    internal static void SeedTeam(DatabaseContext context, int teamId)
    {
        context.Teams.Add(new TeamEntity { Id = teamId, Name = $"team-{teamId}", Description = string.Empty, AvatarPath = string.Empty });
        context.SaveChanges();
    }
}
