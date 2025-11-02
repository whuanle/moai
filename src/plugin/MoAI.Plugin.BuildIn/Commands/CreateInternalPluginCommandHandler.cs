using MediatR;
using MoAI.Database;
using MoAI.Infra.Exceptions;
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

        var checkResult = await plugin.CheckConfigAsync(request.Params);

        if (checkResult.Count > 0)
        {
            throw new BusinessException("参数配置有误") { StatusCode = 409 };
        }

        var entity = new MoAI.Database.Entities.PluginInternalEntity
        {
            TemplatePluginKey = request.TemplatePluginKey,
            Title = request.Title,
            Description = request.Description,
            ClassifyId = request.ClassifyId,
            IsPublic = request.IsPublic,
            PluginName = pluginInfo.PluginKey,
            Config = request.Params,
        };

        _databaseContext.PluginInternals.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt { Value = entity.Id };
    }
}
