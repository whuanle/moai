using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Static.Plugins;
using MoAI.Infra.Models;
using Xunit;

namespace MoAI.AIPlugin.Static.Tests;

/// <summary>
/// 静态插件 static_current_user 行为测试：上下文缺失/匿名/已认证三种形态的返回.
/// </summary>
public class CurrentUserPluginTests
{
    private sealed class FakeRunContextAccessor : IPluginRunContextAccessor
    {
        public PluginRunContext? Context { get; private set; }

        public PluginRunUser? UserResult { get; set; }

        public int GetUserCallCount { get; private set; }

        public void Set(PluginRunContext? context)
        {
            Context = context;
        }

        public Task<PluginRunUser?> GetUserAsync(CancellationToken cancellationToken)
        {
            GetUserCallCount++;
            return Task.FromResult(UserResult);
        }
    }

    [Fact]
    public async Task RunAsync_WithoutContext_ReturnsNotInjected()
    {
        var accessor = new FakeRunContextAccessor();
        var plugin = new CurrentUserPlugin(accessor);

        var response = await plugin.RunAsync(new(), CancellationToken.None);

        Assert.False(response.IsContextInjected);
        Assert.False(response.IsAuthenticated);
        Assert.Equal(0, response.UserId);
        Assert.Equal(string.Empty, response.UserName);
        Assert.Equal(0, accessor.GetUserCallCount);
    }

    [Fact]
    public async Task RunAsync_WithAnonymousContext_DoesNotLoadUser()
    {
        var accessor = new FakeRunContextAccessor
        {
            UserResult = new PluginRunUser { Id = 1, UserName = "someone" },
        };
        accessor.Set(new PluginRunContext { UserId = 0, UserType = UserType.None, Source = PluginRunSource.Admin });
        var plugin = new CurrentUserPlugin(accessor);

        var response = await plugin.RunAsync(new(), CancellationToken.None);

        Assert.True(response.IsContextInjected);
        Assert.False(response.IsAuthenticated);
        Assert.Equal(0, response.UserId);
        Assert.Equal("admin", response.Source);
        Assert.Equal(0, accessor.GetUserCallCount);
    }

    [Fact]
    public async Task RunAsync_WithAuthenticatedContext_MapsUserFields()
    {
        var accessor = new FakeRunContextAccessor
        {
            UserResult = new PluginRunUser
            {
                Id = 42,
                UserName = "alice",
                NickName = "爱丽丝",
                Email = "alice@example.com",
                IsAdmin = true,
            },
        };
        accessor.Set(new PluginRunContext
        {
            UserId = 42,
            UserType = UserType.Normal,
            TeamId = 7,
            Source = PluginRunSource.Agent,
        });
        var plugin = new CurrentUserPlugin(accessor);

        var response = await plugin.RunAsync(new(), CancellationToken.None);

        Assert.True(response.IsContextInjected);
        Assert.True(response.IsAuthenticated);
        Assert.Equal(42, response.UserId);
        Assert.Equal("alice", response.UserName);
        Assert.Equal("爱丽丝", response.NickName);
        Assert.Equal("alice@example.com", response.Email);
        Assert.True(response.IsAdmin);
        Assert.Equal("normal", response.UserType);
        Assert.Equal(7, response.TeamId);
        Assert.Equal("agent", response.Source);
        Assert.Equal(1, accessor.GetUserCallCount);
    }
}
