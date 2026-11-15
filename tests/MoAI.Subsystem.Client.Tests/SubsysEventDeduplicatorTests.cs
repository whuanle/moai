using MoAI.Subsystem.Client.Events;
using Xunit;

namespace MoAI.Subsystem.Client.Tests;

/// <summary>
/// 事件去重与本地过滤/重连退避配置。
/// </summary>
public class SubsysEventDeduplicatorTests
{
    [Fact]
    public void 去重窗口按容量淘汰最旧事件()
    {
        var deduplicator = new InMemorySubsysEventDeduplicator(capacity: 2);

        deduplicator.MarkSeen("a");
        deduplicator.MarkSeen("b");
        Assert.True(deduplicator.IsSeen("a"));
        Assert.True(deduplicator.IsSeen("b"));

        deduplicator.MarkSeen("c");
        Assert.False(deduplicator.IsSeen("a"));
        Assert.True(deduplicator.IsSeen("b"));
        Assert.True(deduplicator.IsSeen("c"));
    }

    [Fact]
    public void 容量为零时关闭去重()
    {
        var deduplicator = new InMemorySubsysEventDeduplicator(capacity: 0);
        deduplicator.MarkSeen("a");

        Assert.False(deduplicator.IsSeen("a"));
    }

    [Fact]
    public void 清除后不再命中()
    {
        var deduplicator = new InMemorySubsysEventDeduplicator();
        deduplicator.MarkSeen("a");
        deduplicator.Clear();

        Assert.False(deduplicator.IsSeen("a"));
    }

    [Fact]
    public void 本地过滤支持前缀白名单与排除优先级()
    {
        var options = new SubsysEventClientOptions
        {
            EventPrefixes = { SubsysEventNames.TeamPrefix, SubsysEventNames.UserPrefix },
            ExcludedEvents = { SubsysEventNames.UserUpdated },
            ExcludedEventPrefixes = { "subsys.user.oauth_" },
        };

        Assert.True(options.IsEventAccepted(SubsysEventNames.TeamCreated));
        Assert.True(options.IsEventAccepted(SubsysEventNames.UserCreated));
        Assert.False(options.IsEventAccepted(SubsysEventNames.UserUpdated));
        Assert.False(options.IsEventAccepted(SubsysEventNames.UserOAuthLinked));
        Assert.False(options.IsEventAccepted(SubsysEventNames.OrgRoleCreated));
    }

    [Fact]
    public void 未配置白名单时接收全部事件()
    {
        var options = new SubsysEventClientOptions();

        Assert.True(options.IsEventAccepted(SubsysEventNames.AccessAppDeleted));
        Assert.False(options.IsEventAccepted(string.Empty));
    }

    [Fact]
    public void 重连退避指数增长并封顶()
    {
        var options = new SubsysEventClientOptions
        {
            ReconnectInitialDelaySeconds = 2,
            ReconnectMaxDelaySeconds = 5,
        };

        Assert.Equal(TimeSpan.Zero, options.GetReconnectDelay(0));
        Assert.Equal(TimeSpan.FromSeconds(2), options.GetReconnectDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(4), options.GetReconnectDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(5), options.GetReconnectDelay(3));
        Assert.Equal(TimeSpan.FromSeconds(5), options.GetReconnectDelay(20));
    }
}
