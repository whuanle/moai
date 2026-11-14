using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Message.Commands;

namespace MoAI.Message.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteMessageCommand"/>.
/// </summary>
public class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteMessageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteMessageCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
    {
        // 仅允许删除本人的消息（软删除，由框架写入 is_deleted）
        await _databaseContext.SoftDeleteAsync(_databaseContext.Messages
            .Where(x => x.RecipientUserId == request.ContextUserId && request.MessageIds.Contains(x.Id)));

        return EmptyCommandResponse.Default;
    }
}
