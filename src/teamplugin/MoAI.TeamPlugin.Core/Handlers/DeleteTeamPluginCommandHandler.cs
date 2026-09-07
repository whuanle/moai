using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Commands;
using System.Transactions;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteTeamPluginCommand"/>
/// </summary>
public class DeleteTeamPluginCommandHandler : IRequestHandler<DeleteTeamPluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTeamPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="storageService">文件存储领域服务.</param>
    public DeleteTeamPluginCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteTeamPluginCommand request, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(request.TeamId, request.ContextUserId, cancellationToken);

        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId && x.TeamId == request.TeamId && x.IsDeleted == 0, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("团队插件不存在") { StatusCode = 404 };
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
        else
        {
            var dynamicEntity = await _databaseContext.PluginDynamics
                .FirstOrDefaultAsync(x => x.Id == pluginEntity.PluginId && x.IsDeleted == 0, cancellationToken);

            if (dynamicEntity != null)
            {
                dynamicEntity.IsDeleted = 1;
                _databaseContext.Update(dynamicEntity);
            }
        }

        _databaseContext.Plugins.Remove(pluginEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }

    private async Task EnsureManagerAsync(long teamId, long userId, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理插件.") { StatusCode = 403 };
        }
    }
}
