using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.LoginSafety.Commands;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 解封 IP：DB 标记解封 + 删除 Redis 判定缓存.
/// </summary>
public class LiftIpBanCommandHandler : IRequestHandler<LiftIpBanCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiftIpBanCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="redisDatabase">Redis.</param>
    public LiftIpBanCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(LiftIpBanCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.LoginIpBans
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("封禁记录不存在") { StatusCode = 404 };
        }

        if (entity.IsLifted)
        {
            throw new BusinessException("该封禁记录已解封") { StatusCode = 400 };
        }

        entity.IsLifted = true;
        entity.LiftUserId = request.ContextUserId;
        entity.LiftTime = DateTimeOffset.Now;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _redisDatabase.Database.KeyDeleteAsync($"ls:ipban:{entity.Ip}");
        return EmptyCommandResponse.Default;
    }
}
