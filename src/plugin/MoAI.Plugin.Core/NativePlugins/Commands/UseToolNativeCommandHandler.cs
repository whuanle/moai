using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Plugin.Models;
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
            throw new BusinessException("不能创建非工具模板插件实例") { StatusCode = 409 };
        }

        // 数据库唯一
        var existPlugin = await _databaseContext.PluginNatives
            .AnyAsync(p => p.TemplatePluginKey == request.TemplatePluginKey, cancellationToken);

        if (existPlugin)
        {
            throw new BusinessException("工具类插件只能有一个实例") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var pluginNativeEntity = new PluginNativeEntity
        {
            TemplatePluginClassify = pluginInfo.Classify.ToJsonString(),
            TemplatePluginKey = request.TemplatePluginKey,
            Config = "{}"
        };

        await _databaseContext.PluginNatives.AddAsync(pluginNativeEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        var pluginEntitiy = new PluginEntity()
        {
            PluginName = pluginInfo.Key,
            Title = pluginInfo.Name,
            Type = (int)PluginType.ToolPlugin,
            IsPublic = request.IsPublic,
            ClassifyId = request.ClassifyId,
            PluginId = pluginNativeEntity.Id,
            Description = pluginInfo.Description
        };

        await _databaseContext.Plugins.AddAsync(pluginEntitiy, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
