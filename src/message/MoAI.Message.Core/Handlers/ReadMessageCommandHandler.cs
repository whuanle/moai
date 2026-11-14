using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Message.Commands;

namespace MoAI.Message.Handlers;

/// <summary>
/// <inheritdoc cref="ReadMessageCommand"/>.
/// </summary>
public class ReadMessageCommandHandler : IRequestHandler<ReadMessageCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadMessageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ReadMessageCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ReadMessageCommand request, CancellationToken cancellationToken)
    {
        // 仅允许标记本人且未读的消息，软删除行被全局过滤器排除
        await _databaseContext.WhereUpdateAsync(
            _databaseContext.Messages
                .Where(x => x.RecipientUserId == request.ContextUserId && !x.IsRead && request.MessageIds.Contains(x.Id)),
            setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadTime, DateTimeOffset.Now));

        return EmptyCommandResponse.Default;
    }
}
