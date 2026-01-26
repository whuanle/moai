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
public class PluginNodeRuntime : INodeRuntime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNodeRuntime"/> class.
    /// </summary>
    /// <param name="serviceProvider">服务提供者，用于解析插件实例.</param>
    /// <param name="databaseContext">数据库上下文，用于查询插件配置.</param>
    /// <param name="nativePluginFactory">插件工厂，用于获取插件模板信息.</param>
    /// <param name="mediator">MediatR 中介者，用于发送命令.</param>
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
    /// 根据插件类型（Tool 或 Native）调用相应的插件执行逻辑.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据，应包含 pluginKey 和 params 字段.</param>
    /// <param name="context">工作流上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含插件执行结果的执行结果.</returns>
    public async Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("pluginKey", out var pluginKeyObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: pluginKey");
            }

            string pluginKey = pluginKeyObj?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pluginKey))
            {
                return NodeExecutionResult.Failure("pluginKey 不能为空");
            }

            // 2. 获取插件模板信息
            var pluginInfo = _nativePluginFactory.GetPluginByKey(pluginKey);
            if (pluginInfo == null)
            {
                return NodeExecutionResult.Failure($"未找到插件模板: {pluginKey}");
            }

            // 3. 解析插件服务
            var service = _serviceProvider.GetService(pluginInfo.Type);
            if (service == null)
            {
                return NodeExecutionResult.Failure($"无法解析插件服务: {pluginKey}");
            }

            // 4. 解析插件参数
            string pluginParams = string.Empty;
            if (inputs.TryGetValue("params", out var paramsObj) && paramsObj != null)
            {
                // 如果参数是字符串，直接使用
                if (paramsObj is string paramsStr)
                {
                    pluginParams = paramsStr;
                }
                // 如果参数是字典或对象，序列化为 JSON
                else
                {
                    pluginParams = System.Text.Json.JsonSerializer.Serialize(paramsObj);
                }
            }

            // 5. 插件用量统计
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

            // 6. 根据插件类型执行
            string result;
            if (pluginInfo.PluginType == PluginType.ToolPlugin)
            {
                // Tool 插件直接执行
                result = await ExecuteToolPluginAsync(service, pluginParams, cancellationToken);
            }
            else
            {
                // Native 插件需要加载配置
                result = await ExecuteNativePluginAsync(service, pluginKey, inputs, pluginParams, cancellationToken);
            }

            // 7. 构建输出
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

    /// <summary>
    /// 执行 Tool 插件.
    /// Tool 插件无需配置，直接调用 TestAsync 方法.
    /// </summary>
    /// <param name="service">插件服务实例.</param>
    /// <param name="pluginParams">插件参数 JSON 字符串.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>插件执行结果.</returns>
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

    /// <summary>
    /// 执行 Native 插件.
    /// Native 插件需要先加载配置，然后调用 TestAsync 方法.
    /// </summary>
    /// <param name="service">插件服务实例.</param>
    /// <param name="pluginKey">插件 Key.</param>
    /// <param name="inputs">节点输入数据.</param>
    /// <param name="pluginParams">插件参数 JSON 字符串.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>插件执行结果.</returns>
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
            // 1. 获取插件配置
            // 优先使用输入中的 pluginId
            int? pluginId = null;
            if (inputs.TryGetValue("pluginId", out var pluginIdObj) && pluginIdObj != null)
            {
                pluginId = Convert.ToInt32(pluginIdObj);
            }

            // 如果没有 pluginId，尝试通过 pluginKey 查找默认插件实例
            if (!pluginId.HasValue)
            {
                // 查找该模板的第一个可用插件实例
                var pluginEntity = await _databaseContext.PluginNatives
                    .Where(x => x.TemplatePluginKey == pluginKey && x.IsDeleted == 0)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (pluginEntity != null)
                {
                    pluginId = pluginEntity.Id;
                }
            }

            // 如果找到了插件实例，加载配置
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

            // 2. 执行插件
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
