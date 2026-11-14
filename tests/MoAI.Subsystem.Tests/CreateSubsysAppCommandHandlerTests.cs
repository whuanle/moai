using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Handlers;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 创建系统级接入命令处理器测试（子系统自填 appid / 应用接入自动生成 appid）.
/// </summary>
public class CreateSubsysAppCommandHandlerTests
{
    [Fact]
    public async Task Handle_SubsystemAllTeams_CreatesReversibleSecretAndMask()
    {
        using var db = TestSqliteContext.Create();
        var cipher = TestSeeds.CreateCipher();
        var handler = new CreateSubsysAppCommandHandler(db.Context, cipher, TestSeeds.CreateCodec());

        var response = await handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "智能报表",
            Description = "报表子系统",
            TeamScope = SubsystemTeamScope.AllTeams,
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        // 响应只回掩码，不回原文（原文可随时经 reveal-secret 信封查看）
        Assert.Contains("****", response.SecretMask, StringComparison.Ordinal);

        var entity = await db.Context.AccessApps.FindAsync(response.Id);
        Assert.NotNull(entity);
        Assert.Equal("report", entity!.Key);
        Assert.Equal(MoAI.Database.Enums.AccessAppLevel.System, entity.Level);
        Assert.Equal(MoAI.Database.Enums.AccessAppType.Subsystem, entity.AppType);
        Assert.Equal(AccessAppStatus.Enabled, entity.Status);
        Assert.Equal(0, entity.TeamId);

        // 库内 AES 可逆密文落库（v2: 前缀），掩码与原文一致，可解密回显
        Assert.StartsWith("v2:", entity.Secret, StringComparison.Ordinal);
        Assert.True(cipher.TryReveal(entity.Secret, out var secret));
        Assert.Equal(48, secret.Length);
        Assert.Equal(cipher.Mask(secret), response.SecretMask);

        // 全员范围不写授权行
        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == entity.Id).ToList());
    }

    [Fact]
    public async Task Handle_SubsystemSpecifiedTeams_WritesDistinctAuths()
    {
        using var db = TestSqliteContext.Create();
        TestSeeds.SeedTeam(db.Context, 1);
        TestSeeds.SeedTeam(db.Context, 2);
        var handler = new CreateSubsysAppCommandHandler(db.Context, TestSeeds.CreateCipher(), TestSeeds.CreateCodec());

        var response = await handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "智能报表",
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [1, 2, 2],
        }, CancellationToken.None);

        var auths = db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == response.Id).ToList();
        Assert.Equal([1, 2], auths.Select(x => x.TeamId).OrderBy(x => x).ToList());
    }

    [Fact]
    public async Task Handle_SubsystemDuplicateKey_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSubsysAppCommandHandler(db.Context, TestSeeds.CreateCipher(), TestSeeds.CreateCodec());
        await handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "a",
            TeamScope = SubsystemTeamScope.AllTeams,
        }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "b",
            TeamScope = SubsystemTeamScope.AllTeams,
        }, CancellationToken.None));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_SubsystemMissingTeam_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSubsysAppCommandHandler(db.Context, TestSeeds.CreateCipher(), TestSeeds.CreateCodec());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "a",
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [998, 999],
        }, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("998", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_App_GeneratesKeyPrefixAndClearsSubsystemFields()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSubsysAppCommandHandler(db.Context, TestSeeds.CreateCipher(), TestSeeds.CreateCodec());

        var response = await handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.App,
            Name = "ERP 组织同步",
            Key = "ignored",
            CallbackUrls = ["https://erp.local/cb"],
        }, CancellationToken.None);

        // 应用接入 appid 自动生成（moai-sa- 前缀），回调等子系统专属字段被忽略
        Assert.StartsWith("moai-sa-", response.Key, StringComparison.Ordinal);
        // 结构化 appid：35 字符，HMAC 可本地校验并反解主键
        Assert.Equal(35, response.Key.Length);
        var codec = TestSeeds.CreateCodec();
        Assert.True(codec.TryDecode(response.Key, out var decodedId));
        Assert.Equal(response.Id, decodedId);
        Assert.Equal(12, response.KeyPrefix.Length);
        Assert.Equal(MoAI.Database.Enums.AccessAppType.App, response.AppType);

        var entity = await db.Context.AccessApps.FindAsync(response.Id);
        Assert.NotNull(entity);
        Assert.Equal("[]", entity!.CallbackUrls);
        Assert.Equal(SubsystemTeamScope.AllTeams, entity.TeamScope);
        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == entity.Id).ToList());
    }

    [Fact]
    public async Task Handle_WithRegisteredAvatarPath_PersistsAvatar()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Files.Add(new MoAI.Database.Entities.FileEntity
        {
            Id = 1,
            ObjectKey = "images/avatar-ok.png",
            FileExtension = ".png",
            FileSha256 = new string('a', 64),
            FileSize = 8,
            ContentType = "image/png",
            IsUploaded = true,
        });
        await db.Context.SaveChangesAsync();
        var handler = new CreateSubsysAppCommandHandler(db.Context, TestSeeds.CreateCipher(), TestSeeds.CreateCodec());

        var response = await handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "智能报表",
            TeamScope = SubsystemTeamScope.AllTeams,
            AvatarPath = "images/avatar-ok.png",
        }, CancellationToken.None);

        var entity = await db.Context.AccessApps.FindAsync(response.Id);
        Assert.NotNull(entity);
        Assert.Equal("images/avatar-ok.png", entity!.AvatarPath);
    }

    [Fact]
    public async Task Handle_WithUnregisteredAvatarPath_Throws404()
    {
        using var db = TestSqliteContext.Create();
        // 未登记/未完成上传的 objectKey 一律拒绝（防伪造，@SUB-S68）
        db.Context.Files.Add(new MoAI.Database.Entities.FileEntity
        {
            Id = 2,
            ObjectKey = "images/avatar-pending.png",
            FileExtension = ".png",
            FileSha256 = new string('a', 64),
            FileSize = 8,
            ContentType = "image/png",
            IsUploaded = false,
        });
        await db.Context.SaveChangesAsync();
        var handler = new CreateSubsysAppCommandHandler(db.Context, TestSeeds.CreateCipher(), TestSeeds.CreateCodec());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "a",
            TeamScope = SubsystemTeamScope.AllTeams,
            AvatarPath = "images/avatar-pending.png",
        }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);

        var exGhost = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "a",
            TeamScope = SubsystemTeamScope.AllTeams,
            AvatarPath = "images/ghost.png",
        }, CancellationToken.None));
        Assert.Equal(404, exGhost.StatusCode);
    }

    [Fact]
    public async Task Handle_WithoutAvatarPath_PersistsEmpty()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSubsysAppCommandHandler(db.Context, TestSeeds.CreateCipher(), TestSeeds.CreateCodec());

        var response = await handler.Handle(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.App,
            Name = "ERP 组织同步",
        }, CancellationToken.None);

        var entity = await db.Context.AccessApps.FindAsync(response.Id);
        Assert.NotNull(entity);
        Assert.Equal(string.Empty, entity!.AvatarPath);
    }

    [Fact]
    public void Validate_AvatarPathMaxLength255()
    {
        var validator = new InlineValidator<CreateSubsysAppCommand>();
        CreateSubsysAppCommand.Validate(validator);

        Assert.True(validator.Validate(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "a",
            AvatarPath = new string('a', 255),
        }).IsValid);
        Assert.False(validator.Validate(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "a",
            AvatarPath = new string('a', 256),
        }).IsValid);
    }

    [Fact]
    public void Validate_SubsystemRequiresKey_ForbidsMoaiPrefix()
    {
        var validator = new InlineValidator<CreateSubsysAppCommand>();
        CreateSubsysAppCommand.Validate(validator);

        // 子系统必须自填 appid 且不能使用 moai- 保留前缀
        Assert.False(validator.Validate(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = string.Empty,
            Name = "a",
        }).IsValid);
        Assert.False(validator.Validate(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "moai-sa-x",
            Name = "a",
        }).IsValid);

        // 组织授权维度加入后，指定团队空清单为合法配置（仅组织授权生效，@SUB-S46）
        Assert.True(validator.Validate(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.Subsystem,
            Key = "report",
            Name = "a",
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [],
        }).IsValid);

        // 应用接入无需自填 appid
        Assert.True(validator.Validate(new CreateSubsysAppCommand
        {
            AppType = MoAI.Database.Enums.AccessAppType.App,
            Name = "a",
        }).IsValid);
    }
}
