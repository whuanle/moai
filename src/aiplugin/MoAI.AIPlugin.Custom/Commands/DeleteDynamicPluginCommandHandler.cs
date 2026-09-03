using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteDynamicPluginCommand"/> 软删除动态插件实例及关联 plugin 行.
/// </summary>
public class DeleteDynamicPluginCommandHandler : IRequestHandler<DeleteDynamicPluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteDynamicPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteDynamicPluginCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteDynamicPluginCommand request, CancellationToken cancellationToken)
    {
        var dynamicEntity = await _databaseContext.PluginDynamics
            .FirstOrDefaultAsync(x => x.PluginKey == request.PluginKey && x.IsDeleted == 0, cancellationToken);

        if (dynamicEntity == null)
        {
            throw new BusinessException("动态插件实例不存在") { StatusCode = 404 };
        }

        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.PluginId == dynamicEntity.Id && x.IsDeleted == 0, cancellationToken);

        if (pluginEntity != null)
        {
            pluginEntity.IsDeleted = 1;
            _databaseContext.Plugins.Update(pluginEntity);
        }

        dynamicEntity.IsDeleted = 1;
        _databaseContext.PluginDynamics.Update(dynamicEntity);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
