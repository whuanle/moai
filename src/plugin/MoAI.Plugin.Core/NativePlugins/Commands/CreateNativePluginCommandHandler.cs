using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Commands;
using MoAI.Plugin.Plugins;
using System.Transactions;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="CreateNativePluginCommand"/>
/// </summary>
public class CreateNativePluginCommandHandler : IRequestHandler<CreateNativePluginCommand, SimpleInt>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateNativePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="nativePluginFactory"></param>
    public CreateNativePluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, INativePluginFactory nativePluginFactory)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateNativePluginCommand request, CancellationToken cancellationToken)
    {
        var pluginInfo = _nativePluginFactory.GetPluginByKey(request.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        if (pluginInfo.PluginType != PluginType.NativePlugin)
        {
            throw new BusinessException("不能创建工具模板插件实例") { StatusCode = 409 };
        }

        var service = _serviceProvider.GetService(pluginInfo.Type);
        if (service is null)
        {
            throw new BusinessException("不能实例化插件实例") { StatusCode = 404 };
        }

        var plugin = service as INativePluginRuntime;

        var checkResult = await plugin!.CheckConfigAsync(request.Config);

        if (!string.IsNullOrEmpty(checkResult))
        {
            throw new BusinessException(checkResult) { StatusCode = 409 };
        }

        // 不能跟内置工具插件重名
        var pluginTemplates = _nativePluginFactory.GetPlugins();
        if (pluginTemplates.Any(x => x.PluginType == PluginType.ToolPlugin && x.Key == request.Name))
        {
            throw new BusinessException("不能跟内置工具插件重名") { StatusCode = 409 };
        }

        // 检查插件是否同名
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == request.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var pluginNativeEntity = new PluginNativeEntity
        {
            TemplatePluginClassify = pluginInfo.Classify.ToJsonString(),
            TemplatePluginKey = request.TemplatePluginKey,
            Config = request.Config ?? "{}"
        };

        await _databaseContext.PluginNatives.AddAsync(pluginNativeEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        var pluginEntitiy = new PluginEntity()
        {
            PluginName = request.Name,
            Title = request.Title,
            Type = (int)PluginType.NativePlugin,
            IsPublic = request.IsPublic,
            ClassifyId = request.ClassifyId,
            PluginId = pluginNativeEntity.Id,
            Description = request.Description
        };

        await _databaseContext.Plugins.AddAsync(pluginEntitiy, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return new SimpleInt { Value = pluginEntitiy.Id };
    }
}
