using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.LoginSafety.Commands;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 手动封禁 IP：写 DB 封禁记录 + Redis 判定缓存.
/// </summary>
public class AddIpBanCommandHandler : IRequestHandler<AddIpBanCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddIpBanCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="redisDatabase">Redis.</param>
    public AddIpBanCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AddIpBanCommand request, CancellationToken cancellationToken)
    {
        var active = await _databaseContext.LoginIpBans
            .AnyAsync(x => x.Ip == request.Ip && !x.IsLifted && x.ExpireTime > DateTimeOffset.Now, cancellationToken);
        if (active)
        {
            throw new BusinessException("该 IP 已存在生效中的封禁记录") { StatusCode = 409 };
        }

        var now = DateTimeOffset.Now;
        _databaseContext.LoginIpBans.Add(new LoginIpBanEntity
        {
            Id = Guid.CreateVersion7(),
            Ip = request.Ip,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "管理员手动封禁" : request.Reason,
            SourceRule = "manual",
            ExpireTime = now.AddMinutes(request.Minutes),
            IsLifted = false,
            LiftUserId = 0,
            CreateTime = now
        });
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _redisDatabase.Database.StringSetAsync($"ls:ipban:{request.Ip}", "1", TimeSpan.FromMinutes(request.Minutes));
        return EmptyCommandResponse.Default;
    }
}
