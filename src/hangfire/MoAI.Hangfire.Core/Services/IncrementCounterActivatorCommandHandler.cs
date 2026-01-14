using FluentValidation;
using MediatR;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 实现计数器.
/// </summary>
public class IncrementCounterActivatorCommandHandler : IRequestHandler<IncrementCounterActivatorCommand>
{
    private readonly IValidator<IncrementCounterActivatorCommand> _validator;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementCounterActivatorCommandHandler"/> class.
    /// </summary>
    /// <param name="validator"></param>
    /// <param name="redisDatabase"></param>
    public IncrementCounterActivatorCommandHandler(IValidator<IncrementCounterActivatorCommand> validator, IRedisDatabase redisDatabase)
    {
        _validator = validator;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task Handle(IncrementCounterActivatorCommand request, CancellationToken cancellationToken)
    {
        if (request.Counters.Count == 0)
        {
            return;
        }

        _validator.ValidateAndThrow(request);

        List<Task> tasks = new();
        var batch = _redisDatabase.Database.CreateBatch();
        foreach (var item in request.Counters)
        {
            var task = batch.HashIncrementAsync($"counter:{request.Name}", item.Key, item.Value);
            tasks.Add(task);
        }

        batch.Execute();

        await Task.WhenAll(tasks);
    }
}