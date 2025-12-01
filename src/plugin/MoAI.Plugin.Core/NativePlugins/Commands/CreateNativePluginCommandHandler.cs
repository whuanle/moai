using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.NativePlugins.Commands;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="CreateNativePluginCommand"/>
/// </summary>
public class CreateNativePluginCommandHandler : IRequestHandler<CreateNativePluginCommand, SimpleInt>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateNativePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    public CreateNativePluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateNativePluginCommand request, CancellationToken cancellationToken)
    {
        var pluginInfo = NativePluginFactory.Plugins.FirstOrDefault(x => x.Key == request.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        if (pluginInfo.IsTool)
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

        // 检查插件是否同名
        var exists = await _databaseContext.PluginNatives
            .AnyAsync(x => x.PluginName == request.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == request.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        var entity = new MoAI.Database.Entities.PluginNativeEntity
        {
            TemplatePluginClassify = pluginInfo.Classify.ToJsonString(),
            TemplatePluginKey = request.TemplatePluginKey,
            Title = request.Title,
            Description = request.Description,
            ClassifyId = request.ClassifyId,
            IsPublic = request.IsPublic,
            PluginName = pluginInfo.Key,
            Config = request.Config ?? string.Empty
        };

        _databaseContext.PluginNatives.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt { Value = entity.Id };
    }
}
