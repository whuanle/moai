using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Commands.Responses;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 事件目录与新响应契约：事件名唯一/前缀规范、目录查询与心跳响应。
/// </summary>
public class SubsystemEventContractTests
{
    [Fact]
    public void 事件目录_事件名唯一且统一前缀()
    {
        var names = SubsystemEventCatalog.All.Select(x => x.Event).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.StartsWith("subsys.", name, StringComparison.Ordinal));
        Assert.All(SubsystemEventCatalog.All, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Audience));
            Assert.NotEmpty(item.DataFields);
        });
    }

    [Fact]
    public async Task 目录查询_返回信封版本与保留窗口()
    {
        var response = await new QuerySubsysEventCatalogCommandHandler().Handle(
            new QuerySubsysEventCatalogCommand { SubsystemId = Guid.CreateVersion7() },
            CancellationToken.None);

        Assert.Equal(SubsystemEventContract.SchemaVersion, response.SchemaVersion);
        Assert.Equal(SubsystemEventContract.RetentionDays, response.RetentionDays);
        Assert.Same(SubsystemEventCatalog.All, response.Items);
    }

    [Fact]
    public async Task 心跳响应_回显在线实例数与建议间隔()
    {
        var appId = Guid.CreateVersion7();
        var online = new FakeOnlineService();
        var handler = new SubsystemHeartbeatCommandHandler(online);

        var response = await handler.Handle(
            new SubsystemHeartbeatCommand { SubsystemId = appId, InstanceId = "i-1" },
            CancellationToken.None);

        Assert.Equal(appId, online.TouchedAppId);
        Assert.Equal("i-1", online.TouchedInstanceId);
        Assert.Equal(3, response.OnlineInstances);
        Assert.Equal(SubsystemHeartbeatCommandResponse.DefaultOnlineWindowSeconds, response.WindowSeconds);
        Assert.Equal(SubsystemHeartbeatCommandResponse.DefaultRecommendedIntervalSeconds, response.RecommendedIntervalSeconds);
        Assert.NotEqual(default, response.ServerTime);
    }

    private sealed class FakeOnlineService : ISubsystemOnlineService
    {
        public Guid TouchedAppId { get; private set; }

        public string? TouchedInstanceId { get; private set; }

        public Task TouchAsync(Guid subsysAppId, string instanceId, CancellationToken cancellationToken = default)
        {
            TouchedAppId = subsysAppId;
            TouchedInstanceId = instanceId;
            return Task.CompletedTask;
        }

        public Task<int> GetOnlineInstanceCountAsync(Guid subsysAppId) => Task.FromResult(3);

        public Task<bool> IsOnlineAsync(Guid subsysAppId) => Task.FromResult(true);

        public Task RemoveAsync(Guid subsysAppId) => Task.CompletedTask;
    }
}
