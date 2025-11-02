using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Login.Handlers;

/// <summary>
/// 刷新超级管理员列表.
/// </summary>
public class RefreshAdminsCommandHandler : IRequestHandler<RefreshAdminsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshAdminsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="redisDatabase"></param>
    public RefreshAdminsCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RefreshAdminsCommand request, CancellationToken cancellationToken)
    {
        await _redisDatabase.Database.KeyDeleteAsync("adminids");
        await _redisDatabase.Database.KeyDeleteAsync("rootid");

        var rootId = await _databaseContext.Settings.FirstOrDefaultAsync(x => x.Key == "root");

        if (rootId == null)
        {
            throw new BusinessException("系统未设置超级管理员.");
        }

        var adminIds = await _databaseContext.Users.Where(u => u.IsAdmin).Select(x => x.Id.ToString()).ToListAsync();

        adminIds.Add(rootId.Value);

        await _redisDatabase.SetAddAllAsync("adminids", StackExchange.Redis.CommandFlags.None, adminIds.ToArray());
        await _redisDatabase.Database.StringSetAsync("rootid", rootId.Value);

        return EmptyCommandResponse.Default;
    }
}
