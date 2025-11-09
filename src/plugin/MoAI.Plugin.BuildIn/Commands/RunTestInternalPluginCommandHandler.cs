using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.InternalPluginCommands;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="RunTestInternalPluginCommand"/>
/// </summary>
public class RunTestInternalPluginCommandHandler : IRequestHandler<RunTestInternalPluginCommand, RunTestInternalPluginCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunTestInternalPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    public RunTestInternalPluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<RunTestInternalPluginCommandResponse> Handle(RunTestInternalPluginCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.PluginInternals.FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("未找到插件实例") { StatusCode = 404 };
        }

        // 一种是不需要实例化的插件模板，一种是需要实例化的插件模板
        var pluginInfo = InternalPluginFactory.Plugins.FirstOrDefault(x => x.Key == entity.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var service = _serviceProvider.GetService(pluginInfo.Type);
        if (service is null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var plugin = (service as IInternalPluginRuntime)!;

        // 执行测试
        try
        {
            await plugin.ImportConfigAsync(entity.Config!);
            var result = await plugin!.TestAsync(request.Params);
            return new RunTestInternalPluginCommandResponse
            {
                IsSuccess = true,
                Response = result,
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException("运行插件出现异常", ex);
        }
    }
}
