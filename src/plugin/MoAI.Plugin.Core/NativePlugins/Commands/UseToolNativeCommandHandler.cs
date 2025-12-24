using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Transactions;

namespace MoAI.Plugin.NativePlugins.Commands;

/// <summary>
/// <inheritdoc cref="UseToolNativeCommand"/>
/// </summary>
public class UseToolNativeCommandHandler : IRequestHandler<UseToolNativeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseToolNativeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="nativePluginFactory"></param>
    public UseToolNativeCommandHandler(DatabaseContext databaseContext, INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UseToolNativeCommand request, CancellationToken cancellationToken)
    {
        var pluginInfo = _nativePluginFactory.GetPluginByKey(request.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        if (pluginInfo.PluginType != PluginType.ToolPlugin)
        {
            throw new BusinessException("只有工具类插件可以执行此操作") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var toolPlugin = await _databaseContext.PluginNatives
            .FirstOrDefaultAsync(p => p.TemplatePluginKey == request.TemplatePluginKey, cancellationToken);

        if (toolPlugin == null)
        {
            toolPlugin = new PluginNativeEntity
            {
                TemplatePluginClassify = pluginInfo.Classify.ToJsonString(),
                TemplatePluginKey = request.TemplatePluginKey,
                Config = "{}"
            };

            await _databaseContext.PluginNatives.AddAsync(toolPlugin, cancellationToken);
            await _databaseContext.SaveChangesAsync();

            var pluginEntitiy = new PluginEntity()
            {
                PluginName = pluginInfo.Key,
                Title = pluginInfo.Name,
                Type = (int)PluginType.ToolPlugin,
                IsPublic = true,
                ClassifyId = request.ClassifyId,
                PluginId = toolPlugin.Id,
                Description = pluginInfo.Description
            };

            await _databaseContext.Plugins.AddAsync(pluginEntitiy, cancellationToken);
            await _databaseContext.SaveChangesAsync();
        }
        else
        {
            var pluginEntitiy = await _databaseContext.Plugins.FirstOrDefaultAsync(x => x.PluginId == toolPlugin.Id);
            if (pluginEntitiy == null)
            {
                pluginEntitiy = new PluginEntity()
                {
                    PluginName = pluginInfo.Key,
                    Title = pluginInfo.Name,
                    Type = (int)PluginType.ToolPlugin,
                    IsPublic = true,
                    ClassifyId = request.ClassifyId,
                    PluginId = toolPlugin.Id,
                    Description = pluginInfo.Description
                };

                await _databaseContext.Plugins.AddAsync(pluginEntitiy, cancellationToken);
                await _databaseContext.SaveChangesAsync();
            }
            else
            {
                pluginEntitiy.ClassifyId = request.ClassifyId;
                _databaseContext.Update(pluginEntitiy);
                await _databaseContext.SaveChangesAsync();
            }
        }

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
