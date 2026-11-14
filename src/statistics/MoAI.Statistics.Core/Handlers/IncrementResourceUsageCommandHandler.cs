using FluentValidation;
using MediatR;
using MoAI.Statistics.Services;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Statistics.Handlers;

/// <summary>
/// <inheritdoc cref="IncrementResourceUsageCommand"/>
/// </summary>
public class IncrementResourceUsageCommandHandler : IRequestHandler<IncrementResourceUsageCommand>
{
    private readonly IValidator<IncrementResourceUsageCommand> _validator;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementResourceUsageCommandHandler"/> class.
    /// </summary>
    /// <param name="validator">请求校验器.</param>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public IncrementResourceUsageCommandHandler(IValidator<IncrementResourceUsageCommand> validator, IRedisDatabase redisDatabase)
    {
        _validator = validator;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task Handle(IncrementResourceUsageCommand request, CancellationToken cancellationToken)
    {
        if (request.Counters.Count == 0)
        {
            return;
        }

        _validator.ValidateAndThrow(request);

        RedisValue[] arguments = new RedisValue[request.Counters.Count * 2];
        var index = 0;
        foreach (var item in request.Counters)
        {
            arguments[index++] = item.Key;
            arguments[index++] = item.Value;
        }

        const string Script = """
            for i = 1, #ARGV, 2 do
                redis.call('HINCRBY', KEYS[1], ARGV[i], ARGV[i + 1])
            end
            return 1
            """;
        await _redisDatabase.ScriptEvaluateAsync(
            Script,
            [new RedisKey($"counter:{request.Name}")],
            arguments);
    }
}
