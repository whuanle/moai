using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// <see cref="AppExternalChannelHelper"/> 单测：飞书/钉钉外部渠道反查与对话链接生成.
/// </summary>
public class AppExternalChannelHelperTests
{
    private static readonly Guid AppId = Guid.CreateVersion7();

    [Fact]
    public async Task 飞书绑定_生成applink单聊地址_域名按连接配置推导()
    {
        using var db = TestSqliteContext.Create();
        await SeedAppAsync(db.Context);
        var feishuAppId = Guid.CreateVersion7();
        db.Context.FeishuApps.Add(new FeishuAppEntity
        {
            Id = feishuAppId,
            TeamId = 1,
            Name = "fs",
            Description = string.Empty,
            AppId = "cli_test001",
            AppSecret = "s",
            Domain = "https://open.larksuite.com",
            IsDisable = false,
        });
        db.Context.FeishuAppBindings.Add(Binding(feishuAppId));
        await db.Context.SaveChangesAsync();

        var map = await AppExternalChannelHelper.GetChannelsAsync(db.Context, new[] { AppId }, CancellationToken.None);

        var channels = Assert.Single(map[AppId]);
        Assert.Equal(AppExternalChannelType.Feishu, channels.ChannelType);
        Assert.Equal("https://applink.larksuite.com/client/bot/open?appId=cli_test001", channels.ChatUrl);
    }

    [Fact]
    public async Task 未知域名_回退飞书默认applink()
    {
        using var db = TestSqliteContext.Create();
        await SeedAppAsync(db.Context);
        var feishuAppId = Guid.CreateVersion7();
        db.Context.FeishuApps.Add(new FeishuAppEntity
        {
            Id = feishuAppId,
            TeamId = 1,
            Name = "fs",
            Description = string.Empty,
            AppId = "cli_x",
            AppSecret = "s",
            Domain = "https://example.com",
            IsDisable = false,
        });
        db.Context.FeishuAppBindings.Add(Binding(feishuAppId));
        await db.Context.SaveChangesAsync();

        var map = await AppExternalChannelHelper.GetChannelsAsync(db.Context, new[] { AppId }, CancellationToken.None);

        Assert.Equal("https://applink.feishu.cn/client/bot/open?appId=cli_x", Assert.Single(map[AppId]).ChatUrl);
    }

    [Fact]
    public async Task 钉钉绑定已回填chatbotUserId_生成jumprobot地址()
    {
        using var db = TestSqliteContext.Create();
        await SeedAppAsync(db.Context);
        var dingTalkAppId = Guid.CreateVersion7();
        db.Context.DingTalkApps.Add(new DingTalkAppEntity
        {
            Id = dingTalkAppId,
            TeamId = 1,
            Name = "dt",
            Description = string.Empty,
            AppKey = "ding001",
            AppSecret = "s",
            ChatbotUserId = "$:LWCP_v1:$abc=",
            IsDisable = false,
        });
        db.Context.DingTalkAppBindings.Add(DingBinding(dingTalkAppId));
        await db.Context.SaveChangesAsync();

        var map = await AppExternalChannelHelper.GetChannelsAsync(db.Context, new[] { AppId }, CancellationToken.None);

        var channels = Assert.Single(map[AppId]);
        Assert.Equal(AppExternalChannelType.Dingtalk, channels.ChannelType);
        Assert.Equal("dingtalk://dingtalkclient/action/jumprobot?dingtalkid=%24%3ALWCP_v1%3A%24abc%3D", channels.ChatUrl);
    }

    [Fact]
    public async Task 钉钉未回填chatbotUserId_不生成入口()
    {
        using var db = TestSqliteContext.Create();
        await SeedAppAsync(db.Context);
        var dingTalkAppId = Guid.CreateVersion7();
        db.Context.DingTalkApps.Add(new DingTalkAppEntity
        {
            Id = dingTalkAppId,
            TeamId = 1,
            Name = "dt",
            Description = string.Empty,
            AppKey = "ding001",
            AppSecret = "s",
            ChatbotUserId = string.Empty,
            IsDisable = false,
        });
        db.Context.DingTalkAppBindings.Add(DingBinding(dingTalkAppId));
        await db.Context.SaveChangesAsync();

        var map = await AppExternalChannelHelper.GetChannelsAsync(db.Context, new[] { AppId }, CancellationToken.None);

        // 钉钉无按 AppKey 拉起机器人单聊的深链，未回填 dingtalk id 前不露出入口
        Assert.False(map.TryGetValue(AppId, out var channels) && channels.Any(x => x.ChannelType == AppExternalChannelType.Dingtalk));
    }

    [Fact]
    public async Task 双渠道同应用_两条渠道并存()
    {
        using var db = TestSqliteContext.Create();
        await SeedAppAsync(db.Context);
        var feishuAppId = Guid.CreateVersion7();
        db.Context.FeishuApps.Add(new FeishuAppEntity
        {
            Id = feishuAppId,
            TeamId = 1,
            Name = "fs",
            Description = string.Empty,
            AppId = "cli_y",
            AppSecret = "s",
            Domain = "https://open.feishu.cn",
            IsDisable = false,
        });
        db.Context.FeishuAppBindings.Add(Binding(feishuAppId));
        var dingTalkAppId = Guid.CreateVersion7();
        db.Context.DingTalkApps.Add(new DingTalkAppEntity
        {
            Id = dingTalkAppId,
            TeamId = 1,
            Name = "dt",
            Description = string.Empty,
            AppKey = "ding002",
            AppSecret = "s",
            ChatbotUserId = "$:LWCP_v1:$xyz=",
            IsDisable = false,
        });
        db.Context.DingTalkAppBindings.Add(DingBinding(dingTalkAppId));
        await db.Context.SaveChangesAsync();

        var map = await AppExternalChannelHelper.GetChannelsAsync(db.Context, new[] { AppId }, CancellationToken.None);

        Assert.Equal(2, map[AppId].Count);
        Assert.Contains(map[AppId], x => x.ChannelType == AppExternalChannelType.Feishu);
        Assert.Contains(map[AppId], x => x.ChannelType == AppExternalChannelType.Dingtalk);
    }

    [Fact]
    public async Task 连接禁用或渠道不属于目标应用_忽略()
    {
        using var db = TestSqliteContext.Create();
        await SeedAppAsync(db.Context);
        var disabledId = Guid.CreateVersion7();
        db.Context.FeishuApps.Add(new FeishuAppEntity
        {
            Id = disabledId,
            TeamId = 1,
            Name = "fs-off",
            Description = string.Empty,
            AppId = "cli_off",
            AppSecret = "s",
            Domain = "https://open.feishu.cn",
            IsDisable = true,
        });
        db.Context.FeishuAppBindings.Add(Binding(disabledId));
        // 绑定到其它应用的渠道
        var otherAppId = Guid.CreateVersion7();
        var feishuAppId = Guid.CreateVersion7();
        db.Context.FeishuApps.Add(new FeishuAppEntity
        {
            Id = feishuAppId,
            TeamId = 1,
            Name = "fs-other",
            Description = string.Empty,
            AppId = "cli_other",
            AppSecret = "s",
            Domain = "https://open.feishu.cn",
            IsDisable = false,
        });
        db.Context.FeishuAppBindings.Add(Binding(feishuAppId, channelId: otherAppId.ToString("D")));
        await db.Context.SaveChangesAsync();

        var map = await AppExternalChannelHelper.GetChannelsAsync(db.Context, new[] { AppId }, CancellationToken.None);

        Assert.Empty(map);
    }

    private static FeishuAppBindingEntity Binding(Guid feishuAppId, string? channelId = null)
    {
        return new FeishuAppBindingEntity
        {
            FeishuAppId = feishuAppId,
            ChannelType = (int)FeishuChannelType.App,
            ChannelId = channelId ?? AppId.ToString("D"),
        };
    }

    private static DingTalkAppBindingEntity DingBinding(Guid dingTalkAppId, string? channelId = null)
    {
        return new DingTalkAppBindingEntity
        {
            DingTalkAppId = dingTalkAppId,
            ChannelType = (int)DingTalkChannelType.App,
            ChannelId = channelId ?? AppId.ToString("D"),
        };
    }

    private static async Task SeedAppAsync(Database.DatabaseContext db)
    {
        db.Apps.Add(new AppEntity
        {
            Id = AppId,
            TeamId = 1,
            Name = "demo",
            Description = string.Empty,
            Avatar = string.Empty,
            AppType = (int)AppType.Agent,
            IsExternal = false,
            ClassifyId = 0,
            PublishStatus = 1,
        });
        await db.SaveChangesAsync();
    }
}
