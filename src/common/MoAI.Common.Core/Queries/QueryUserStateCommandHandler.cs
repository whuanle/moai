using MediatR;
using MoAI.Login.Commands;
using MoAI.Login.Queries;
using MoAI.Login.Queries.Responses;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Common.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserStateCommand"/>
/// </summary>
public class QueryUserStateCommandHandler : IRequestHandler<QueryUserStateCommand, UserStateInfo>
{
    private readonly IRedisDatabase _redisDatabase;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserStateCommandHandler"/> class.
    /// </summary>
    /// <param name="redisDatabase"></param>
    /// <param name="mediator"></param>
    public QueryUserStateCommandHandler(IRedisDatabase redisDatabase, IMediator mediator)
    {
        _redisDatabase = redisDatabase;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> Handle(QueryUserStateCommand request, CancellationToken cancellationToken)
    {
        var key = $"userstate:{request.UserId}";
        var userState = await _redisDatabase.GetAsync<UserStateInfo>(key);
        if (userState == null)
        {
            var refreshResponse = await _mediator.Send(new RefreshUserStateCommand
            {
                UserId = request.UserId
            });

            return refreshResponse;
        }

        return userState;
    }
}
