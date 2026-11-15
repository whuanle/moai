using Microsoft.Extensions.Logging.Abstractions;
using MoAI.Subsystem.Client.Events;
using Xunit;

namespace MoAI.Subsystem.Client.Tests;

/// <summary>
/// 事件后台宿主：随应用启动/停止自动管理连接循环。
/// </summary>
public class SubsysEventWorkerTests
{
    [Fact]
    public async Task Worker_随宿主启动与停止事件客户端()
    {
        var client = new FakeEventClient();
        var worker = new SubsysEventWorker(client, NullLogger<SubsysEventWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => client.StartCount == 1);
        Assert.True(client.Started);

        await worker.StopAsync(CancellationToken.None);
        Assert.True(client.Stopped);
        await client.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private sealed class FakeEventClient : ISubsysEventClient
    {
        public event Func<SubsysEventItem, Task>? EventReceived
        {
            add { }
            remove { }
        }

        public event Func<SubsysReadyItem, Task>? Ready
        {
            add { }
            remove { }
        }

        public event Func<Exception?, Task>? Disconnected
        {
            add { }
            remove { }
        }

        public event Func<string, Task>? ResyncRequired
        {
            add { }
            remove { }
        }

        public int StartCount { get; private set; }

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public bool IsConnected => false;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Stopped = true;
            return Task.CompletedTask;
        }

        public Task WaitForConnectedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
