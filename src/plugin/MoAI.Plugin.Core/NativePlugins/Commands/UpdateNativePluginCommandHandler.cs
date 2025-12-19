using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Commands;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="UpdateNativePluginCommand"/>
/// </summary>
public class UpdateNativePluginCommandHandler : IRequestHandler<UpdateNativePluginCommand, EmptyCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNativePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="nativePluginFactory"></param>
    public UpdateNativePluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, INativePluginFactory nativePluginFactory)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateNativePluginCommand request, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins.FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("未找到插件实例") { StatusCode = 404 };
        }

        var pluginNativeEntity = await _databaseContext.PluginNatives.FirstOrDefaultAsync(x => x.Id == pluginEntity.PluginId, cancellationToken);

        if (pluginNativeEntity == null)
        {
            throw new BusinessException("未找到插件实例") { StatusCode = 404 };
        }

        var pluginInfo = _nativePluginFactory.GetPluginByKey(pluginNativeEntity.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        if (pluginInfo.PluginType == Models.PluginType.NativePlugin)
        {
            var service = _serviceProvider.GetService(pluginInfo.Type);
            if (service is null)
            {
                throw new BusinessException("未找到插件模板") { StatusCode = 404 };
            }

            var plugin = service as INativePluginRuntime;

            var checkResult = await plugin!.CheckConfigAsync(request.Config!);

            if (!string.IsNullOrEmpty(checkResult))
            {
                throw new BusinessException(checkResult) { StatusCode = 409 };
            }
        }

        // 不能跟内置工具插件重名
        var pluginTemplates = _nativePluginFactory.GetPlugins();
        if (pluginTemplates.Any(x => x.PluginType == PluginType.ToolPlugin && x.Key == request.Name))
        {
            throw new BusinessException("不能跟内置工具插件重名") { StatusCode = 409 };
        }

        // 检查插件是否同名
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == request.Name && x.Id != request.PluginId, cancellationToken);
        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        if (pluginInfo.PluginType == Models.PluginType.NativePlugin)
        {
            pluginEntity.PluginName = request.Name;
            pluginEntity.Title = request.Title;
            pluginEntity.Description = request.Description;
            pluginEntity.IsPublic = request.IsPublic;

            pluginNativeEntity.Config = request.Config;
        }

        pluginEntity.ClassifyId = request.ClassifyId;

        _databaseContext.Plugins.Update(pluginEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}