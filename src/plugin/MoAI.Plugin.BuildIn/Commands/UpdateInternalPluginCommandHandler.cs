using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.BuiltCommands;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="UpdateInternalPluginCommand"/>
/// </summary>
public class UpdateInternalPluginCommandHandler : IRequestHandler<UpdateInternalPluginCommand, EmptyCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateInternalPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    public UpdateInternalPluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateInternalPluginCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.PluginInternals.FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("未找到插件实例") { StatusCode = 404 };
        }

        if (!InternalPluginFactory.Plugins.TryGetValue(entity.TemplatePluginKey, out var pluginInfo))
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var service = _serviceProvider.GetService(pluginInfo.Type);
        if (service is null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var plugin = service as IInternalPluginRuntime;

        var checkResult = await plugin!.CheckConfigAsync(request.Config);

        if (!string.IsNullOrEmpty(checkResult))
        {
            throw new BusinessException(checkResult) { StatusCode = 409 };
        }

        // 检查插件是否同名
        var exists = await _databaseContext.PluginInternals
            .AnyAsync(x => x.PluginName == request.Name && x.Id != request.PluginId, cancellationToken);
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

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.ClassifyId = request.ClassifyId;
        entity.IsPublic = request.IsPublic;
        entity.Config = request.Config;

        _databaseContext.PluginInternals.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}