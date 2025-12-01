using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.NativePlugins.Commands;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="RunTestNativePluginCommand"/>
/// </summary>
public class RunTestNativePluginCommandHandler : IRequestHandler<RunTestNativePluginCommand, RunTestNativePluginCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunTestNativePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    public RunTestNativePluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<RunTestNativePluginCommandResponse> Handle(RunTestNativePluginCommand request, CancellationToken cancellationToken)
    {
        var pluginInfo = NativePluginFactory.Plugins.FirstOrDefault(x => x.Key == request.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var service = _serviceProvider.GetService(pluginInfo.Type);
        if (service is null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        if (pluginInfo.IsTool)
        {
            // 执行测试
            try
            {
                var plugin = (service as IToolPluginRuntime)!;
                var result = await plugin!.TestAsync(request.Params);
                return new RunTestNativePluginCommandResponse
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
        else
        {
            var plugin = (service as INativePluginRuntime)!;

            var entity = await _databaseContext.PluginNatives.FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

            if (entity == null)
            {
                throw new BusinessException("未找到插件实例") { StatusCode = 404 };
            }

            // 执行测试
            try
            {
                await plugin.ImportConfigAsync(entity.Config!);
                var result = await plugin!.TestAsync(request.Params);
                return new RunTestNativePluginCommandResponse
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
}
