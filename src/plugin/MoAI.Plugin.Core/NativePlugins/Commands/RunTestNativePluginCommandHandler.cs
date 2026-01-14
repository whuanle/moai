using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Hangfire.Services;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.NativePlugins.Commands;
using MoAI.Plugin.NativePlugins.Models;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="RunTestNativePluginCommand"/>
/// </summary>
public class RunTestNativePluginCommandHandler : IRequestHandler<RunTestNativePluginCommand, RunTestNativePluginCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunTestNativePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="nativePluginFactory"></param>
    /// <param name="mediator"></param>
    public RunTestNativePluginCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, INativePluginFactory nativePluginFactory, IMediator mediator)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<RunTestNativePluginCommandResponse> Handle(RunTestNativePluginCommand request, CancellationToken cancellationToken)
    {
        var pluginInfo = _nativePluginFactory.GetPluginByKey(request.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var service = _serviceProvider.GetService(pluginInfo.Type);
        if (service is null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        // 插件用量统计
        await _mediator.Send(new IncrementCounterActivatorCommand
        {
            Name = "plugin",
            Counters = new Dictionary<string, int>
            {
                { pluginInfo.Key, 1 },
            },
        });

        if (pluginInfo.PluginType == PluginType.ToolPlugin)
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

            var pluginEntity = await _databaseContext
                .PluginNatives.Where(x => x.Id == _databaseContext.Plugins.First(a => a.Id == request.PluginId).PluginId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pluginEntity == null)
            {
                throw new BusinessException("未找到插件实例") { StatusCode = 404 };
            }

            // 执行测试
            try
            {
                await plugin.ImportConfigAsync(pluginEntity.Config!);
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
