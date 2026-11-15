using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// SignalR 事件客户端实现：主动外连 MoAI，按 appId 分组接收；断线后按当前 lastEventId 重连补发。
/// 处理顺序：控制事件 → 去重 → 本地过滤 → 回调 → 推进游标（失败不推进，重连可重试）。
/// </summary>
public sealed class SubsysEventClient : ISubsysEventClient
{
    private const string OnEvent = "onEvent";
    private const string OnReady = "onReady";

    private readonly MoaiSubsysClientOptions _options;
    private readonly ISubsysTokenProvider _tokenProvider;
    private readonly ISubsysEventCursorStore _cursorStore;
    private readonly ISubsysEventDeduplicator _deduplicator;
    private readonly ILogger<SubsysEventClient> _logger;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private HubConnection? _connection;
    private TaskCompletionSource _connectedSignal = CreateSignal();
    private volatile bool _stopping;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysEventClient"/> class.
    /// </summary>
    /// <param name="options">客户端配置.</param>
    /// <param name="tokenProvider">机器 token 提供器.</param>
    /// <param name="cursorStore">lastEventId 存储.</param>
    /// <param name="deduplicator">事件去重器.</param>
    /// <param name="logger">日志.</param>
    public SubsysEventClient(
        IOptions<MoaiSubsysClientOptions> options,
        ISubsysTokenProvider tokenProvider,
        ISubsysEventCursorStore cursorStore,
        ISubsysEventDeduplicator deduplicator,
        ILogger<SubsysEventClient> logger)
    {
        _options = options.Value;
        _tokenProvider = tokenProvider;
        _cursorStore = cursorStore;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public event Func<SubsysEventItem, Task>? EventReceived;

    /// <inheritdoc/>
    public event Func<SubsysReadyItem, Task>? Ready;

    /// <inheritdoc/>
    public event Func<Exception?, Task>? Disconnected;

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

            _stopping = false;
            _connectedSignal = CreateSignal();
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
            _stopping = true;
            loopTask = _loopTask;
            connection = _connection;
            _loopTask = null;
            _connection = null;
            if (_loopCts is not null)
            {
                await _loopCts.CancelAsync();
                _loopCts.Dispose();
            }

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
    public async Task WaitForConnectedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsConnected)
        {
            return;
        }

        await _connectedSignal.Task.WaitAsync(timeout, cancellationToken);
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
            var started = false;
            try
            {
                connection = await BuildConnectionAsync(cancellationToken);
                connection.On<SubsysEventItem>(OnEvent, HandleEventAsync);
                connection.On<SubsysReadyItem>(OnReady, HandleReadyAsync);

                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.Closed += async ex =>
                {
                    if (ex is not null)
                    {
                        _logger.LogWarning(ex, "接入事件连接断开.");
                    }

                    closed.TrySetResult();
                    _connectedSignal = CreateSignal();
                    await RaiseDisconnectedAsync(ex);
                };

                await connection.StartAsync(cancellationToken);
                _connection = connection;
                started = true;
                attempt = 0;
                _connectedSignal.TrySetResult();
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
                if (!started)
                {
                    await RaiseDisconnectedAsync(ex);
                }
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
                await Task.Delay(_options.Events.GetReconnectDelay(attempt++), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<HubConnection> BuildConnectionAsync(CancellationToken cancellationToken)
    {
        var hubPath = NormalizeHubPath(_options.Events.HubPath);
        var url = $"{_options.BaseUrl.TrimEnd('/')}{hubPath}";
        var lastEventId = await _cursorStore.GetLastEventIdAsync(cancellationToken);
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
            if (item.IsResyncRequired())
            {
                await RaiseResyncAsync(item.GetResyncReason() ?? "unknown");

                // 控制事件 id 不是补发游标；清空后下一次连接按首次连接走全量对账
                _deduplicator.Clear();
                await _cursorStore.ClearLastEventIdAsync();
                return;
            }

            var eventId = item.Id;
            if (!string.IsNullOrEmpty(eventId) && _deduplicator.IsSeen(eventId))
            {
                _logger.LogDebug("忽略重复接入事件. Event={Event}, Id={Id}", item.Event, eventId);
                return;
            }

            // 本地过滤只减少回调噪音，仍然推进游标，避免重连时反复补发被过滤事件
            if (_options.Events.IsEventAccepted(item.Event) && EventReceived is not null)
            {
                await EventReceived(item);
            }

            if (!string.IsNullOrEmpty(eventId))
            {
                await _cursorStore.SaveLastEventIdAsync(eventId);
                _deduplicator.MarkSeen(eventId);
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

    private async Task RaiseDisconnectedAsync(Exception? exception)
    {
        if (_stopping || Disconnected is null)
        {
            return;
        }

        try
        {
            await Disconnected(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理接入事件断线回调失败.");
        }
    }

    private static string NormalizeHubPath(string? hubPath)
    {
        var path = string.IsNullOrWhiteSpace(hubPath) ? "/hub/subsys" : hubPath.Trim();
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static TaskCompletionSource CreateSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
