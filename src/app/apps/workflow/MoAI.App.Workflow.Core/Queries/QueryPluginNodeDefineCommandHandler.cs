using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Plugin;
using MoAI.Plugin.Plugins;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginNodeDefineCommand"/>
/// </summary>
public class QueryPluginNodeDefineCommandHandler : IRequestHandler<QueryPluginNodeDefineCommand, IReadOnlyCollection<NodeDefineItem>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginNodeDefineCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="nativePluginFactory">原生插件工厂.</param>
    public QueryPluginNodeDefineCommandHandler(DatabaseContext databaseContext, INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<NodeDefineItem>> Handle(QueryPluginNodeDefineCommand request, CancellationToken cancellationToken)
    {
        var pluginIds = request.PluginIds.ToList();
        var nodes = new List<NodeDefineItem>();

        // 批量查询插件信息
        var plugins = await _databaseContext.Plugins
            .Where(p => pluginIds.Contains(p.Id) && p.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        foreach (var plugin in plugins)
        {
            var inputFields = new List<FieldDefine>();
            var outputFields = new List<FieldDefine>();

            // Type: 0=Native, 1=Tool, 2=Custom(MCP), 3=Custom(OpenAPI)
            switch (plugin.Type)
            {
                case 0: // Native Plugin
                    await ParseNativePluginFieldsAsync(plugin.PluginId, inputFields, outputFields, cancellationToken);
                    break;
                case 1: // Tool Plugin
                    await ParseToolPluginFieldsAsync(plugin.PluginId, inputFields, outputFields, cancellationToken);
                    break;
                case 2: // Custom Plugin (MCP)
                case 3: // Custom Plugin (OpenAPI)
                    await ParseCustomPluginFieldsAsync(plugin.PluginId, inputFields, outputFields, cancellationToken);
                    break;
                default:
                    throw new BusinessException($"不支持的插件类型: {plugin.Type}") { StatusCode = 400 };
            }

            nodes.Add(new NodeDefineItem
            {
                NodeType = NodeType.Plugin,
                NodeTypeName = "插件节点",
                Description = $"执行插件: {plugin.Title}",
                PluginId = plugin.Id,
                PluginName = plugin.Title,
                InputFields = inputFields,
                OutputFields = outputFields,
                SupportsStreaming = false,
                Icon = "api",
                Color = "#1890ff"
            });
        }

        return nodes;
    }

    private async Task ParseNativePluginFieldsAsync(int nativePluginId, List<FieldDefine> inputFields, List<FieldDefine> outputFields, CancellationToken cancellationToken)
    {
        var nativePlugin = await _databaseContext.PluginNatives
            .Where(p => p.Id == nativePluginId && p.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (nativePlugin == null)
        {
            return;
        }

        var pluginTemplate = _nativePluginFactory.GetPluginByKey(nativePlugin.TemplatePluginKey);
        if (pluginTemplate == null)
        {
            return;
        }

        foreach (var fieldTemplate in pluginTemplate.ParamsFieldTemplates)
        {
            inputFields.Add(new FieldDefine
            {
                FieldName = fieldTemplate.Key,
                FieldType = ConvertPluginFieldTypeToWorkflowFieldType(fieldTemplate.FieldType),
                IsRequired = fieldTemplate.IsRequired,
                Description = fieldTemplate.Description
            });
        }

        outputFields.Add(new FieldDefine
        {
            FieldName = "result",
            FieldType = FieldType.Dynamic,
            IsRequired = true,
            Description = "插件执行结果"
        });
    }

    private async Task ParseToolPluginFieldsAsync(int toolPluginId, List<FieldDefine> inputFields, List<FieldDefine> outputFields, CancellationToken cancellationToken)
    {
        var toolPlugin = await _databaseContext.PluginTools
            .Where(p => p.Id == toolPluginId && p.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (toolPlugin == null)
        {
            return;
        }

        var pluginTemplate = _nativePluginFactory.GetPluginByKey(toolPlugin.TemplatePluginKey);
        if (pluginTemplate == null)
        {
            return;
        }

        foreach (var fieldTemplate in pluginTemplate.ParamsFieldTemplates)
        {
            inputFields.Add(new FieldDefine
            {
                FieldName = fieldTemplate.Key,
                FieldType = ConvertPluginFieldTypeToWorkflowFieldType(fieldTemplate.FieldType),
                IsRequired = fieldTemplate.IsRequired,
                Description = fieldTemplate.Description
            });
        }

        outputFields.Add(new FieldDefine
        {
            FieldName = "result",
            FieldType = FieldType.Dynamic,
            IsRequired = true,
            Description = "插件执行结果"
        });
    }

    private async Task ParseCustomPluginFieldsAsync(int customPluginId, List<FieldDefine> inputFields, List<FieldDefine> outputFields, CancellationToken cancellationToken)
    {
        var customPlugin = await _databaseContext.PluginCustoms
            .Where(p => p.Id == customPluginId && p.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (customPlugin == null)
        {
            return;
        }

        var functions = await _databaseContext.PluginFunctions
            .Where(f => f.PluginCustomId == customPluginId && f.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        inputFields.Add(new FieldDefine
        {
            FieldName = "functionName",
            FieldType = FieldType.String,
            IsRequired = true,
            Description = "要调用的函数名称"
        });

        inputFields.Add(new FieldDefine
        {
            FieldName = "parameters",
            FieldType = FieldType.Object,
            IsRequired = false,
            Description = "函数参数（JSON 对象）"
        });

        outputFields.Add(new FieldDefine
        {
            FieldName = "result",
            FieldType = FieldType.Dynamic,
            IsRequired = true,
            Description = "函数执行结果"
        });

        if (functions.Any())
        {
            outputFields.Add(new FieldDefine
            {
                FieldName = "availableFunctions",
                FieldType = FieldType.Array,
                IsRequired = false,
                Description = $"可用函数列表: {string.Join(", ", functions.Select(f => f.Name))}"
            });
        }
    }

    private static FieldType ConvertPluginFieldTypeToWorkflowFieldType(PluginConfigFieldType pluginFieldType)
    {
        return pluginFieldType switch
        {
            PluginConfigFieldType.String => FieldType.String,
            PluginConfigFieldType.Code => FieldType.String,
            PluginConfigFieldType.Number => FieldType.Number,
            PluginConfigFieldType.Integer => FieldType.Number,
            PluginConfigFieldType.Boolean => FieldType.Boolean,
            PluginConfigFieldType.Object => FieldType.Object,
            PluginConfigFieldType.Map => FieldType.Object,
            PluginConfigFieldType.Array => FieldType.Array,
            _ => FieldType.Dynamic
        };
    }
}
