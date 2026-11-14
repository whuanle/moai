using System.Globalization;
using System.Text.Json;
using MoAI.Auth.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Services;

/// <summary>
/// 登录二次验证票据服务实现：Redis 键 login:ticket:{ticket}，5 分钟有效，一次性消费.
/// </summary>
public class LoginChallengeService : ILoginChallengeService
{
    /// <summary>
    /// 票据有效期（分钟）.
    /// </summary>
    public const int TicketTtlMinutes = 5;

    private sealed record TicketState(long UserId, string Factor);

    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginChallengeService"/> class.
    /// </summary>
    public LoginChallengeService(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<string> CreateTicketAsync(long userId, string factor, CancellationToken cancellationToken)
    {
        var ticket = Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);
        await _redisDatabase.Database.StringSetAsync(
            TicketKey(ticket),
            JsonSerializer.Serialize(new TicketState(userId, factor)),
            TimeSpan.FromMinutes(TicketTtlMinutes));
        return ticket;
    }

    /// <inheritdoc/>
    public async Task<LoginTicketPayload?> PeekTicketAsync(string ticket, CancellationToken cancellationToken)
    {
        var json = await _redisDatabase.Database.StringGetAsync(TicketKey(ticket));
        if (json.IsNull || json.IsNullOrEmpty)
        {
            return null;
        }

        var state = JsonSerializer.Deserialize<TicketState>(json.ToString())!;
        return new LoginTicketPayload(state.UserId, state.Factor);
    }

    /// <inheritdoc/>
    public async Task<LoginTicketPayload?> ConsumeTicketAsync(string ticket, CancellationToken cancellationToken)
    {
        var key = TicketKey(ticket);
        var json = await _redisDatabase.Database.StringGetAsync(key);
        if (json.IsNull || json.IsNullOrEmpty)
        {
            return null;
        }

        await _redisDatabase.Database.KeyDeleteAsync(key);
        var state = JsonSerializer.Deserialize<TicketState>(json.ToString())!;
        return new LoginTicketPayload(state.UserId, state.Factor);
    }

    private static string TicketKey(string ticket) => $"login:ticket:{ticket}";
}
