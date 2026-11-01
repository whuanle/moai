using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.LoginSafety.Commands;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 批量标记风险事件为已读.
/// </summary>
public class MarkRiskEventReadCommandHandler : IRequestHandler<MarkRiskEventReadCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkRiskEventReadCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public MarkRiskEventReadCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(MarkRiskEventReadCommand request, CancellationToken cancellationToken)
    {
        var events = await _databaseContext.LoginRiskEvents
            .Where(x => !x.IsRead && request.EventIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (events.Count > 0)
        {
            foreach (var item in events)
            {
                item.IsRead = true;
            }

            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
