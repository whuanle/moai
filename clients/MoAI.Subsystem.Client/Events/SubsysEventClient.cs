using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// SignalR 事件客户端实现：主动外连 MoAI，按 appId 分组接收；断线后按当前 lastEventId 重连补发.
/// </summary>
public sealed class SubsysEventClient : ISubsysEventClient
{
    private const string HubPath = "/hub/subsys";
    private const string OnEvent = "onEvent";
    private const string OnReady = "onReady";
    private const string ResyncEvent = "subsys.resync_required";

    private readonly MoaiSubsysClientOptions _options;
    private readonly ISubsysTokenProvider _tokenProvider;
    private readonly ISubsysEventCursorStore _cursorStore;
    private readonly ILogger<SubsysEventClient> _logger;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private HubConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysEventClient"/> class.
    /// </summary>
    /// <param name="options">客户端配置.</param>
    /// <param name="tokenProvider">机器 token 提供器.</param>
    /// <param name="cursorStore">lastEventId 存储.</param>
    /// <param name="logger">日志.</param>
    public SubsysEventClient(
        IOptions<MoaiSubsysClientOptions> options,
        ISubsysTokenProvider tokenProvider,
        ISubsysEventCursorStore cursorStore,
        ILogger<SubsysEventClient> logger)
    {
        _options = options.Value;
        _tokenProvider = tokenProvider;
        _cursorStore = cursorStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public event Func<SubsysEventItem, Task>? EventReceived;

    /// <inheritdoc/>
    public event Func<SubsysReadyItem, Task>? Ready;

    /// <inheritdoc/>
    public event Func<string, Task>? ResyncRequired;

    /// <inheritdoc/>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_loopTask is not null)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_loopCts.Token);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? loopTask;
        HubConnection? connection;
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            loopTask = _loopTask;
            connection = _connection;
            _loopTask = null;
            _connection = null;
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (connection is not null)
        {
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "关闭接入事件连接时出现异常.");
            }
        }

        if (loopTask is not null)
        {
            try
            {
                await loopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync();
        _lifecycleGate.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            HubConnection? connection = null;
            try
            {
                connection = BuildConnection();
                connection.On<SubsysEventItem>(OnEvent, HandleEventAsync);
                connection.On<SubsysReadyItem>(OnReady, HandleReadyAsync);

                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.Closed += ex =>
                {
                    if (ex is not null)
                    {
                        _logger.LogWarning(ex, "接入事件连接断开.");
                    }

                    closed.TrySetResult();
                    return Task.CompletedTask;
                };

                await connection.StartAsync(cancellationToken);
                _connection = connection;
                attempt = 0;
                _logger.LogInformation("接入事件连接已建立. AppId={AppId}", _options.AppId);

                using var registration = cancellationToken.Register(() => closed.TrySetCanceled(cancellationToken));
                await closed.Task;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "接入事件连接失败，将重试. AppId={AppId}", _options.AppId);
                _tokenProvider.Invalidate();
            }
            finally
            {
                _connection = null;
                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(Backoff(attempt++), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private HubConnection BuildConnection()
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}{HubPath}";
        var lastEventId = _cursorStore.GetLastEventId();
        if (!string.IsNullOrEmpty(lastEventId))
        {
            url += $"?lastEventId={Uri.EscapeDataString(lastEventId)}";
        }

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = async () => await _tokenProvider.GetAuthorizationTokenAsync(new Uri(url));
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();
    }

    private async Task HandleEventAsync(SubsysEventItem item)
    {
        try
        {
            if (string.Equals(item.Event, ResyncEvent, StringComparison.Ordinal))
            {
                await RaiseResyncAsync(ReadReason(item));
                // 控制事件 id 不是 Stream 游标；清空后下一次连接按首次连接走全量对账
                _cursorStore.ClearLastEventId();
                return;
            }

            if (EventReceived is not null)
            {
                await EventReceived(item);
            }

            if (!string.IsNullOrEmpty(item.Id))
            {
                _cursorStore.SaveLastEventId(item.Id);
            }
        }
        catch (Exception ex)
        {
            // 处理失败不推进游标，下次重连会重新补发
            _logger.LogError(ex, "处理接入事件失败. Event={Event}, Id={Id}", item.Event, item.Id);
        }
    }

    private async Task HandleReadyAsync(SubsysReadyItem ready)
    {
        if (Ready is not null)
        {
            await Ready(ready);
        }

        if (ready.RequiresFullSync)
        {
            await RaiseResyncAsync("initial_sync");
        }
    }

    private async Task RaiseResyncAsync(string reason)
    {
        if (ResyncRequired is not null)
        {
            await ResyncRequired(reason);
        }
    }

    private static string ReadReason(SubsysEventItem item)
    {
        if (item.Data is JsonElement data
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("reason", out var reason)
            && reason.ValueKind == JsonValueKind.String)
        {
            return reason.GetString() ?? "unknown";
        }

        return "unknown";
    }

    private static TimeSpan Backoff(int attempt) => attempt switch
    {
        0 => TimeSpan.Zero,
        1 => TimeSpan.FromSeconds(2),
        2 => TimeSpan.FromSeconds(5),
        3 => TimeSpan.FromSeconds(10),
        _ => TimeSpan.FromSeconds(30),
    };
}
