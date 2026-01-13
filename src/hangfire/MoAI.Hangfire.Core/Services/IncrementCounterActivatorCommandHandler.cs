using FluentValidation;
using MediatR;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 实现计数器.
/// </summary>
public class IncrementCounterActivatorCommandHandler : IRequestHandler<IncrementCounterActivatorCommand, int>
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
    public async Task<int> Handle(IncrementCounterActivatorCommand request, CancellationToken cancellationToken)
    {
        _validator.ValidateAndThrow(request);

        var newValue = await _redisDatabase.HashIncrementByAsync($"counter:{request.Name}", request.Id, request.Count);
        return (int)newValue;
    }
}