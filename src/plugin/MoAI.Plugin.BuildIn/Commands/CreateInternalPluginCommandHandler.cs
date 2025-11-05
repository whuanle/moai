using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.BuiltCommands;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="CreateInternalPluginCommand"/>
/// </summary>
public class CreateInternalPluginCommandHandler : IRequestHandler<CreateInternalPluginCommand, SimpleInt>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateInternalPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    public CreateInternalPluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateInternalPluginCommand request, CancellationToken cancellationToken)
    {
        if (!InternalPluginFactory.Plugins.TryGetValue(request.TemplatePluginKey, out var pluginInfo))
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

        var entity = new MoAI.Database.Entities.PluginInternalEntity
        {
            TemplatePluginClassify = pluginInfo.Classify.ToJsonString(),
            TemplatePluginKey = request.TemplatePluginKey,
            Title = request.Title,
            Description = request.Description,
            ClassifyId = request.ClassifyId,
            IsPublic = request.IsPublic,
            PluginName = pluginInfo.PluginKey,
            Config = request.Config ?? string.Empty,
        };

        _databaseContext.PluginInternals.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt { Value = entity.Id };
    }
}
