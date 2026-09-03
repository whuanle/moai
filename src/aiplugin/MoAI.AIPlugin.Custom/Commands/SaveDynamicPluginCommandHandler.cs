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
/// <inheritdoc cref="SaveDynamicPluginCommand"/> 创建/更新动态插件实例.
/// </summary>
public class SaveDynamicPluginCommandHandler : IRequestHandler<SaveDynamicPluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveDynamicPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    public SaveDynamicPluginCommandHandler(DatabaseContext databaseContext, IPluginRegistry registry)
    {
        _databaseContext = databaseContext;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveDynamicPluginCommand request, CancellationToken cancellationToken)
    {
        var template = _registry.Get(request.TempleteKey);
        if (template == null || !template.IsDynamic)
        {
            throw new BusinessException("动态插件模板不存在") { StatusCode = 404 };
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

        var existing = await _databaseContext.PluginDynamics
            .FirstOrDefaultAsync(x => x.PluginKey == request.PluginKey && x.IsDeleted == 0, cancellationToken);

        if (existing == null)
        {
            await EnsureInstanceKeyUniqueAsync(request.PluginKey, cancellationToken);

            var newDynamic = new PluginDynamicEntity
            {
                Id = Guid.NewGuid(),
                PluginKey = request.PluginKey,
                TempleteKey = request.TempleteKey,
                Config = request.Config,
            };

            var pluginEntity = new PluginEntity
            {
                Id = Guid.NewGuid(),
                IsSystem = true,
                TeamId = 0,
                PluginId = newDynamic.Id,
                PluginName = request.PluginKey,
                Title = request.Title,
                Description = request.Description,
                Type = (int)PluginType.NativePlugin,
                ClassifyId = request.ClassifyId,
                IsPublic = true,
                Counter = 0,
            };

            _databaseContext.PluginDynamics.Add(newDynamic);
            _databaseContext.Plugins.Add(pluginEntity);
        }
        else
        {
            var pluginEntity = await _databaseContext.Plugins
                .FirstOrDefaultAsync(x => x.PluginId == existing.Id && x.IsDeleted == 0, cancellationToken)
                ?? throw new BusinessException("动态插件实例记录不存在") { StatusCode = 404 };

            existing.TempleteKey = request.TempleteKey;
            existing.Config = request.Config;

            pluginEntity.Title = request.Title;
            pluginEntity.Description = request.Description;
            pluginEntity.ClassifyId = request.ClassifyId;

            _databaseContext.PluginDynamics.Update(existing);
            _databaseContext.Plugins.Update(pluginEntity);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task EnsureInstanceKeyUniqueAsync(string pluginKey, CancellationToken cancellationToken)
    {
        if (_registry.Get(pluginKey) != null)
        {
            throw new BusinessException("实例 Key 已被使用") { StatusCode = 409 };
        }

        var dbExists = await _databaseContext.PluginDynamics
            .AnyAsync(x => x.PluginKey == pluginKey && x.IsDeleted == 0, cancellationToken);
        if (dbExists)
        {
            throw new BusinessException("实例 Key 已被使用") { StatusCode = 409 };
        }
    }
}
