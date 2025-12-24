using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Templates.Commands;
using MoAI.Storage.Commands;
using System.Transactions;

namespace MoAI.Plugin.Templates.Handlers;

/// <summary>
/// DeletePluginCommand.
/// </summary>
public class DeletePluginCommandHandler : IRequestHandler<DeletePluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public DeletePluginCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePluginCommand request, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins.FirstOrDefaultAsync(a => a.Id == request.PluginId);
        if (pluginEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        var pluginType = (PluginType)pluginEntity.Type;

        // 工具类插件无法删除
        if (pluginType == PluginType.ToolPlugin)
        {
            throw new BusinessException("工具类插件无法删除") { StatusCode = 400 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        // MCP 和 openapi 插件
        if (pluginType == PluginType.MCP || pluginType == PluginType.OpenApi)
        {
            var pluginCustomEntity = await _databaseContext.PluginCustoms.FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);
            if (pluginCustomEntity != null)
            {
                _databaseContext.PluginCustoms.Remove(pluginCustomEntity);
                await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == request.PluginId));

                if (pluginEntity.Type == (int)PluginType.OpenApi)
                {
                    await _mediator.Send(new DeleteFileCommand
                    {
                        FileIds = new List<int>() { pluginCustomEntity.OpenapiFileId }
                    });
                }
            }
        }

        if (pluginType == PluginType.NativePlugin)
        {
            await _databaseContext.SoftDeleteAsync(_databaseContext.PluginNatives.Where(x => x.Id == request.PluginId));
        }

        _databaseContext.Plugins.Remove(pluginEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
