using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Feishu.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateWikiCrawlerConfigCommand"/>
/// </summary>
public class UpdateWikiCrawlerConfigCommandHandler : IRequestHandler<UpdateWikiCrawlerConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiCrawlerConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWikiCrawlerConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiCrawlerConfigCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WikiPluginConfigs.FirstOrDefaultAsync(x => x.Id == request.ConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("配置不存在") { StatusCode = 404 };
        }

        if (entity.WorkState == (int)WorkerState.Processing || entity.WorkState == (int)WorkerState.Wait)
        {
            throw new BusinessException("当前已有任务在运行中") { StatusCode = 400 };
        }

        entity.Title = request.Title.Trim();
        entity.Config = ((WikiCrawlerConfig)request).ToJsonString();

        _databaseContext.WikiPluginConfigs.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
