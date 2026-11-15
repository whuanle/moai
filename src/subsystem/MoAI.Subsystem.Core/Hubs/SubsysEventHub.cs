using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Events;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Hubs;

/// <summary>
/// 系统级接入事件 Hub（/hub/subsys）：机器 token/appid 鉴权，按 appId 分组单向推送；
/// 建连时按 <c>lastEventId</c> 从 Redis Stream 补发，游标失效则要求全量对账.
/// </summary>
[Authorize(AuthenticationSchemes = SubsystemAuthDefaults.HubScheme)]
[HttpLogging(HttpLoggingFields.None)]
public class SubsysEventHub : Hub
{
    private const string OnEvent = "onEvent";
    private const string OnReady = "onReady";

    private readonly AccessEventStore _eventStore;
    private readonly ILogger<SubsysEventHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysEventHub"/> class.
    /// </summary>
    /// <param name="eventStore">事件补发索引.</param>
    /// <param name="logger">日志.</param>
    public SubsysEventHub(AccessEventStore eventStore, ILogger<SubsysEventHub> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var appIdText = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (!Guid.TryParse(appIdText, out var appId))
        {
            Context.Abort();
            return;
        }

        var httpContext = Context.GetHttpContext();
        var lastEventId = httpContext?.Request.Query["lastEventId"].ToString();

        await Groups.AddToGroupAsync(Context.ConnectionId, AccessEventHubGroups.ForApp(appId));

        await Clients.Caller.SendAsync(OnReady, new AccessEventReadyItem
        {
            AppId = appId.ToString("N"),
            ServerTime = DateTimeOffset.UtcNow,
            RequiresFullSync = string.IsNullOrWhiteSpace(lastEventId),
            ReplayFrom = lastEventId,
        });

        if (!string.IsNullOrWhiteSpace(lastEventId))
        {
            var result = await _eventStore.ReadAsync(
                appId,
                lastEventId,
                SubsystemEventContract.MaxReplay,
                cancellationToken: Context.ConnectionAborted);
            foreach (var item in result.Items)
            {
                await Clients.Caller.SendAsync(OnEvent, item, Context.ConnectionAborted);
            }

            if (result.ResyncRequired || result.HasMore)
            {
                await Clients.Caller.SendAsync(OnEvent, new AccessEventItem
                {
                    Id = Guid.CreateVersion7().ToString("N"),
                    Event = AccessEventNames.ResyncRequired,
                    Time = DateTimeOffset.UtcNow,
                    Data = new Dictionary<string, object?>
                    {
                        ["reason"] = result.ResyncRequired ? "cursor_expired" : "overflow",
                    },
                }, Context.ConnectionAborted);
            }
        }

        _logger.LogInformation("系统级接入事件连接建立. AppId={AppId}, ConnectionId={ConnectionId}, Replay={Replay}", appId, Context.ConnectionId, !string.IsNullOrWhiteSpace(lastEventId));
        await base.OnConnectedAsync();
    }

    /// <inheritdoc/>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(exception, "系统级接入事件连接断开. ConnectionId={ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
