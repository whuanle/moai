using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="SaveStaticPluginCommand"/> 写回/创建静态插件记录.
/// </summary>
public class SaveStaticPluginCommandHandler : IRequestHandler<SaveStaticPluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveStaticPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    public SaveStaticPluginCommandHandler(DatabaseContext databaseContext, IPluginRegistry registry)
    {
        _databaseContext = databaseContext;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveStaticPluginCommand request, CancellationToken cancellationToken)
    {
        var plugin = _registry.Get(request.PluginKey);
        if (plugin == null || plugin.IsDynamic)
        {
            throw new BusinessException("静态插件不存在") { StatusCode = 404 };
        }

        if (request.ClassifyId != 0)
        {
            var classifyExists = await _databaseContext.Classifies
                .AnyAsync(x => x.Id == request.ClassifyId && x.Type == ClassifyTypes.Plugin && x.IsDeleted == 0, cancellationToken);
            if (!classifyExists)
            {
                throw new BusinessException("分类不存在") { StatusCode = 400 };
            }
        }

        var staticEntity = await _databaseContext.PluginStatics
            .FirstOrDefaultAsync(x => x.PluginKey == request.PluginKey && x.IsDeleted == 0, cancellationToken);

        if (staticEntity == null)
        {
            var newStatic = new PluginStaticEntity
            {
                Id = Guid.NewGuid(),
                PluginKey = request.PluginKey,
            };

            var pluginEntity = new PluginEntity
            {
                Id = Guid.NewGuid(),
                IsSystem = true,
                TeamId = 0,
                PluginId = newStatic.Id,
                PluginName = request.PluginKey,
                Title = request.Title,
                Description = request.Description,
                Type = (int)PluginType.NativePlugin,
                ClassifyId = request.ClassifyId,
                IsPublic = true,
                Counter = 0,
            };

            _databaseContext.Plugins.Add(pluginEntity);
            _databaseContext.PluginStatics.Add(newStatic);
        }
        else
        {
            var pluginEntity = await _databaseContext.Plugins
                .FirstOrDefaultAsync(x => x.PluginId == staticEntity.Id && x.IsDeleted == 0, cancellationToken)
                ?? throw new BusinessException("静态插件记录不存在") { StatusCode = 404 };

            pluginEntity.Title = request.Title;
            pluginEntity.Description = request.Description;
            pluginEntity.ClassifyId = request.ClassifyId;
            _databaseContext.Plugins.Update(pluginEntity);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
