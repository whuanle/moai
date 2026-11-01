using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace MoAI.Message.Hubs;

/// <summary>
/// SignalR 用户标识提供者：与 UserContextProvider 同源，取 ClaimTypes.NameIdentifier（JWT sub）作为用户 id，
/// 供 Clients.User(userId) 定向推送与多实例 Redis backplane 归组.
/// </summary>
public class MoAIUserIdProvider : IUserIdProvider
{
    /// <inheritdoc/>
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
