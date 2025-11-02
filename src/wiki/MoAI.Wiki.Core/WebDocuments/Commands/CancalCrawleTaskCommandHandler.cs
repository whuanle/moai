using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

public class CancalCrawleTaskCommandHandler : IRequestHandler<CancalCrawleTaskCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    public CancalCrawleTaskCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CancalCrawleTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _databaseContext.WikiWebCrawleTasks
            .FirstOrDefaultAsync(x => x.Id == request.TaskId && x.WikiWebConfigId == request.WikiWebConfigId, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("任务不存在.");
        }

        task.CrawleState = (int)CrawleState.Cancal;
        task.Message = "任务已被取消";
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
