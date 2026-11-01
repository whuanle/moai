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
/// <inheritdoc cref="ReadAllMessageCommand"/>.
/// </summary>
public class ReadAllMessageCommandHandler : IRequestHandler<ReadAllMessageCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadAllMessageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ReadAllMessageCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ReadAllMessageCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.WhereUpdateAsync(
            _databaseContext.Messages
                .Where(x => x.RecipientUserId == request.ContextUserId && !x.IsRead),
            setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadTime, DateTimeOffset.Now));

        return EmptyCommandResponse.Default;
    }
}
