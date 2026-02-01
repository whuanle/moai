using Maomi;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Hangfire.Services;
using MoAI.Infra.Exceptions;
using MoAI.Plugin;
using MoAI.Plugin.Plugins;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// Plugin 节点运行时实现.
/// Plugin 节点负责执行插件，支持 Tool 插件和 Native 插件.
/// </summary>
[InjectOnTransient(ServiceKey = NodeType.Plugin)]
public class PluginNodeRuntime : INodeRuntime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNodeRuntime"/> class.
    /// </summary>
    public PluginNodeRuntime(
        IServiceProvider serviceProvider,
        DatabaseContext databaseContext,
        INativePluginFactory nativePluginFactory,
        IMediator mediator)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.Plugin;

    /// <summary>
    /// 执行 Plugin 节点逻辑.
    /// </summary>
    public async Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!inputs.TryGetValue("pluginKey", out var pluginKeyObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: pluginKey");
            }

            string pluginKey = pluginKeyObj?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pluginKey))
            {
                return NodeExecutionResult.Failure("pluginKey 不能为空");
            }

            var pluginInfo = _nativePluginFactory.GetPluginByKey(pluginKey);
            if (pluginInfo == null)
            {
                return NodeExecutionResult.Failure($"未找到插件模板: {pluginKey}");
            }

            var service = _serviceProvider.GetService(pluginInfo.Type);
            if (service == null)
            {
                return NodeExecutionResult.Failure($"无法解析插件服务: {pluginKey}");
            }

            string pluginParams = string.Empty;
            if (inputs.TryGetValue("params", out var paramsObj) && paramsObj != null)
            {
                if (paramsObj is string paramsStr)
                {
                    pluginParams = paramsStr;
                }
                else
                {
                    pluginParams = System.Text.Json.JsonSerializer.Serialize(paramsObj);
                }
            }

            try
            {
                await _mediator.Send(new IncrementCounterActivatorCommand
                {
                    Name = "plugin",
                    Counters = new Dictionary<string, int>
                    {
                        { pluginInfo.Key, 1 },
                    },
                }, cancellationToken);
            }
            catch
            {
                // 统计失败不影响插件执行
            }

            string result;
            if (pluginInfo.PluginType == PluginType.ToolPlugin)
            {
                result = await ExecuteToolPluginAsync(service, pluginParams, cancellationToken);
            }
            else
            {
                result = await ExecuteNativePluginAsync(service, pluginKey, inputs, pluginParams, cancellationToken);
            }

            var output = new Dictionary<string, object>
            {
                ["result"] = result,
                ["pluginKey"] = pluginKey,
                ["pluginName"] = pluginInfo.Name,
                ["pluginType"] = pluginInfo.PluginType.ToString()
            };

            return NodeExecutionResult.Success(output);
        }
        catch (BusinessException bex)
        {
            return NodeExecutionResult.Failure($"业务异常: {bex.Message}");
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure(ex);
        }
    }

    private async Task<string> ExecuteToolPluginAsync(
        object service,
        string pluginParams,
        CancellationToken cancellationToken)
    {
        var plugin = service as IToolPluginRuntime;
        if (plugin == null)
        {
            throw new BusinessException("插件类型不匹配，期望 IToolPluginRuntime");
        }

        try
        {
            var result = await plugin.TestAsync(pluginParams);
            return result;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"执行 Tool 插件失败: {ex.Message}", ex);
        }
    }

    private async Task<string> ExecuteNativePluginAsync(
        object service,
        string pluginKey,
        Dictionary<string, object> inputs,
        string pluginParams,
        CancellationToken cancellationToken)
    {
        var plugin = service as INativePluginRuntime;
        if (plugin == null)
        {
            throw new BusinessException("插件类型不匹配，期望 INativePluginRuntime");
        }

        try
        {
            int? pluginId = null;
            if (inputs.TryGetValue("pluginId", out var pluginIdObj) && pluginIdObj != null)
            {
                pluginId = Convert.ToInt32(pluginIdObj);
            }

            if (!pluginId.HasValue)
            {
                var pluginEntity = await _databaseContext.PluginNatives
                    .Where(x => x.TemplatePluginKey == pluginKey && x.IsDeleted == 0)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (pluginEntity != null)
                {
                    pluginId = pluginEntity.Id;
                }
            }

            if (pluginId.HasValue)
            {
                var pluginNativeEntity = await _databaseContext.PluginNatives
                    .Where(x => x.Id == pluginId.Value)
                    .FirstOrDefaultAsync(cancellationToken);

                if (pluginNativeEntity != null && !string.IsNullOrEmpty(pluginNativeEntity.Config))
                {
                    await plugin.ImportConfigAsync(pluginNativeEntity.Config);
                }
            }

            var result = await plugin.TestAsync(pluginParams);
            return result;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"执行 Native 插件失败: {ex.Message}", ex);
        }
    }
}
