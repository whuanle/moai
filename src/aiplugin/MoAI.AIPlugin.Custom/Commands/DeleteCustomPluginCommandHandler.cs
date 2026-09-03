using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;
using System.Transactions;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteCustomPluginCommand"/>
/// </summary>
public class DeleteCustomPluginCommandHandler : IRequestHandler<DeleteCustomPluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteCustomPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">文件存储领域服务.</param>
    public DeleteCustomPluginCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteCustomPluginCommand request, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var pluginCustomEntity = await _databaseContext.PluginCustoms
            .FirstOrDefaultAsync(x => x.Id == pluginEntity.PluginId, cancellationToken);

        if (pluginCustomEntity != null)
        {
            _databaseContext.PluginCustoms.Remove(pluginCustomEntity);
            await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

            if (pluginCustomEntity.OpenapiFileId != 0)
            {
                await _storageService.DeleteFilesAsync(new[] { pluginCustomEntity.OpenapiFileId }, cancellationToken);
            }
        }

        _databaseContext.Plugins.Remove(pluginEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
