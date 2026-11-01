using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Services;
using MoAI.Database;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="IncrementPluginUsageCommand"/>
/// </summary>
public class IncrementPluginUsageCommandHandler : IRequestHandler<IncrementPluginUsageCommand>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginUsageCounter _usageCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementPluginUsageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="usageCounter">插件使用计数器.</param>
    public IncrementPluginUsageCommandHandler(DatabaseContext databaseContext, IPluginUsageCounter usageCounter)
    {
        _databaseContext = databaseContext;
        _usageCounter = usageCounter;
    }

    /// <inheritdoc/>
    public async Task Handle(IncrementPluginUsageCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Plugins
            .AsNoTracking()
            .Where(x => x.PluginName == request.Key);

        if (request.TeamId > 0)
        {
            query = query.Where(x => x.TeamId == request.TeamId);
        }
        else
        {
            query = query.Where(x => x.TeamId == 0);
        }

        var pluginId = await query
            .OrderBy(x => x.TeamId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (pluginId == null)
        {
            return;
        }

        try
        {
            await _usageCounter.IncrementAsync(pluginId.Value, cancellationToken);
        }
#pragma warning disable CA1031 // 使用统计失败不应影响插件执行
        catch (Exception)
        {
            // 使用统计失败不阻塞插件调用
        }
#pragma warning restore CA1031
    }
}
