using System;
using System.Threading.Tasks;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Service;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 系统级接入单测种子：sqlite 内存库共用的团队/接入行构造与密钥加密服务.
/// </summary>
internal static class TestSeeds
{
    /// <summary>
    /// 单测专用 AES key（任意测试字符串，非真实凭据）.
    /// </summary>
    private const string TestAesKey = "moai-test-aes-key-0001";

    /// <summary>
    /// 构造单测用密钥加密服务.
    /// </summary>
    internal static SubsystemSecretCipher CreateCipher() => new(new AESProvider(TestAesKey));

    /// <summary>
    /// 构造单测用结构化 appid 编解码服务（与密钥服务共用测试 AES key 派生）.
    /// </summary>
    internal static Subsystem.Services.SubsystemAppIdCodec CreateCodec() => new(new MoAI.Infra.SystemOptions { AES = TestAesKey, Storage = new MoAI.Infra.SystemOptionStorage() });

    internal static void SeedTeam(DatabaseContext context, int teamId)
    {
        context.Teams.Add(new TeamEntity { Id = teamId, Name = $"team-{teamId}", Description = string.Empty, AvatarPath = string.Empty });
        context.SaveChanges();
    }

    /// <summary>
    /// 写入一条系统级接入行（access_app，level=system）并按需写团队授权行.
    /// </summary>
    internal static async Task<AccessAppEntity> SeedSubsysAppAsync(
        DatabaseContext context,
        string key,
        AccessAppType appType = AccessAppType.Subsystem,
        SubsystemTeamScope scope = SubsystemTeamScope.AllTeams,
        AccessAppStatus status = AccessAppStatus.Enabled,
        string callback = "",
        string secret = "hash",
        string secretMask = "hash****mask",
        params int[] teamIds)
    {
        var entity = new AccessAppEntity
        {
            Id = Guid.CreateVersion7(),
            Level = AccessAppLevel.System,
            AppType = appType,
            TeamId = 0,
            Key = key,
            KeyPrefix = key[..Math.Min(key.Length, 12)],
            Name = key,
            Description = string.Empty,
            Secret = secret,
            SecretSalt = "salt",
            SecretMask = secretMask,
            Status = status,
            TeamScope = scope,
            CallbackUrls = MoAI.Subsystem.Services.SubsystemCallbacks.Serialize(string.IsNullOrEmpty(callback) ? [] : [callback]),
        };
        context.AccessApps.Add(entity);
        foreach (var teamId in teamIds)
        {
            context.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity { Id = Guid.CreateVersion7(), SubsystemId = entity.Id, TeamId = teamId });
        }

        await context.SaveChangesAsync();
        return entity;
    }
}
