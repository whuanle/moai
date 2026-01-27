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
/// <inheritdoc cref="QueryNodeDefineCommand"/>
/// </summary>
public class QueryNodeDefineCommandHandler : IRequestHandler<QueryNodeDefineCommand, QueryNodeDefineCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNodeDefineCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="nativePluginFactory">原生插件工厂.</param>
    public QueryNodeDefineCommandHandler(DatabaseContext databaseContext, INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<QueryNodeDefineCommandResponse> Handle(QueryNodeDefineCommand request, CancellationToken cancellationToken)
    {
        return request.NodeType switch
        {
            NodeType.Start => GetStartNodeDefine(),
            NodeType.End => GetEndNodeDefine(),
            NodeType.Plugin => await GetPluginNodeDefineAsync(request.PluginId!.Value, cancellationToken),
            NodeType.Wiki => await GetWikiNodeDefineAsync(request.WikiId, cancellationToken),
            NodeType.AiChat => await GetAiChatNodeDefineAsync(request.ModelId, cancellationToken),
            NodeType.Condition => GetConditionNodeDefine(),
            NodeType.ForEach => GetForEachNodeDefine(),
            NodeType.Fork => GetForkNodeDefine(),
            NodeType.JavaScript => GetJavaScriptNodeDefine(),
            NodeType.DataProcess => GetDataProcessNodeDefine(),
            _ => throw new BusinessException($"不支持的节点类型: {request.NodeType}") { StatusCode = 400 }
        };
    }

    private static QueryNodeDefineCommandResponse GetStartNodeDefine()
    {
        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.Start,
            NodeTypeName = "开始节点",
            Description = "工作流的入口点，初始化工作流上下文并传递启动参数",
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "parameters",
                    FieldType = FieldType.Object,
                    IsRequired = false,
                    Description = "启动参数（JSON 对象）"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "parameters",
                    FieldType = FieldType.Object,
                    IsRequired = true,
                    Description = "传递给下一个节点的参数"
                }
            },
            SupportsStreaming = false,
            Icon = "play-circle",
            Color = "#52c41a"
        };
    }

    private static QueryNodeDefineCommandResponse GetEndNodeDefine()
    {
        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.End,
            NodeTypeName = "结束节点",
            Description = "工作流的终点，返回最终输出结果",
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "result",
                    FieldType = FieldType.Dynamic,
                    IsRequired = false,
                    Description = "最终输出结果"
                }
            },
            OutputFields = Array.Empty<FieldDefine>(),
            SupportsStreaming = false,
            Icon = "stop-circle",
            Color = "#ff4d4f"
        };
    }

    private async Task<QueryNodeDefineCommandResponse> GetPluginNodeDefineAsync(int pluginId, CancellationToken cancellationToken)
    {
        // 查询插件信息
        var plugin = await _databaseContext.Plugins
            .Where(p => p.Id == pluginId && p.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException($"插件不存在: {pluginId}") { StatusCode = 404 };
        }

        // 根据插件类型解析输入输出字段定义
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

        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.Plugin,
            NodeTypeName = "插件节点",
            Description = $"执行插件: {plugin.Title}",
            PluginId = pluginId,
            PluginName = plugin.Title,
            InputFields = inputFields,
            OutputFields = outputFields,
            SupportsStreaming = false,
            Icon = "api",
            Color = "#1890ff"
        };
    }

    private async Task ParseNativePluginFieldsAsync(int nativePluginId, List<FieldDefine> inputFields, List<FieldDefine> outputFields, CancellationToken cancellationToken)
    {
        // 查询原生插件详细信息
        var nativePlugin = await _databaseContext.PluginNatives
            .Where(p => p.Id == nativePluginId && p.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (nativePlugin == null)
        {
            return;
        }

        // 从插件工厂获取插件模板信息
        var pluginTemplate = _nativePluginFactory.GetPluginByKey(nativePlugin.TemplatePluginKey);
        if (pluginTemplate == null)
        {
            return;
        }

        // 解析参数字段模板为输入字段
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

        // 原生插件通常返回一个 result 字段
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
        // 查询工具插件详细信息
        var toolPlugin = await _databaseContext.PluginTools
            .Where(p => p.Id == toolPluginId && p.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (toolPlugin == null)
        {
            return;
        }

        // 从插件工厂获取插件模板信息
        var pluginTemplate = _nativePluginFactory.GetPluginByKey(toolPlugin.TemplatePluginKey);
        if (pluginTemplate == null)
        {
            return;
        }

        // 解析参数字段模板为输入字段
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

        // 工具插件通常返回一个 result 字段
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
        // 查询自定义插件详细信息
        var customPlugin = await _databaseContext.PluginCustoms
            .Where(p => p.Id == customPluginId && p.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (customPlugin == null)
        {
            return;
        }

        // 查询插件函数列表
        var functions = await _databaseContext.PluginFunctions
            .Where(f => f.PluginCustomId == customPluginId && f.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        // 对于自定义插件，我们提供通用的输入字段
        // 实际参数由用户在运行时根据选择的函数动态提供
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

        // 自定义插件返回结果
        outputFields.Add(new FieldDefine
        {
            FieldName = "result",
            FieldType = FieldType.Dynamic,
            IsRequired = true,
            Description = "函数执行结果"
        });

        // 如果有可用的函数列表，添加到描述中
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

    private async Task<QueryNodeDefineCommandResponse> GetWikiNodeDefineAsync(int? wikiId, CancellationToken cancellationToken)
    {
        string? wikiName = null;
        if (wikiId.HasValue)
        {
            var wiki = await _databaseContext.Wikis
                .Where(w => w.Id == wikiId.Value && w.IsDeleted == 0)
                .FirstOrDefaultAsync(cancellationToken);

            wikiName = wiki?.Name;
        }

        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.Wiki,
            NodeTypeName = "知识库节点",
            Description = "查询知识库并返回相关文档",
            WikiId = wikiId,
            WikiName = wikiName,
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "query",
                    FieldType = FieldType.String,
                    IsRequired = true,
                    Description = "搜索查询文本"
                },
                new()
                {
                    FieldName = "topK",
                    FieldType = FieldType.Number,
                    IsRequired = false,
                    Description = "返回结果数量（默认 5）"
                },
                new()
                {
                    FieldName = "wikiIds",
                    FieldType = FieldType.Array,
                    IsRequired = false,
                    Description = "指定知识库 ID 列表"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "documents",
                    FieldType = FieldType.Array,
                    IsRequired = true,
                    Description = "相关文档列表"
                },
                new()
                {
                    FieldName = "count",
                    FieldType = FieldType.Number,
                    IsRequired = true,
                    Description = "返回文档数量"
                }
            },
            SupportsStreaming = false,
            Icon = "book",
            Color = "#722ed1"
        };
    }

    private async Task<QueryNodeDefineCommandResponse> GetAiChatNodeDefineAsync(int? modelId, CancellationToken cancellationToken)
    {
        string? modelName = null;
        if (modelId.HasValue)
        {
            var model = await _databaseContext.AiModels
                .Where(m => m.Id == modelId.Value && m.IsDeleted == 0)
                .FirstOrDefaultAsync(cancellationToken);

            modelName = model?.Name;
        }

        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.AiChat,
            NodeTypeName = "AI 对话节点",
            Description = "调用 AI 模型进行对话生成",
            ModelId = modelId,
            ModelName = modelName,
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "prompt",
                    FieldType = FieldType.String,
                    IsRequired = true,
                    Description = "提示词模板"
                },
                new()
                {
                    FieldName = "modelId",
                    FieldType = FieldType.Number,
                    IsRequired = true,
                    Description = "AI 模型 ID"
                },
                new()
                {
                    FieldName = "temperature",
                    FieldType = FieldType.Number,
                    IsRequired = false,
                    Description = "温度参数（0-1）"
                },
                new()
                {
                    FieldName = "maxTokens",
                    FieldType = FieldType.Number,
                    IsRequired = false,
                    Description = "最大生成 Token 数"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "response",
                    FieldType = FieldType.String,
                    IsRequired = true,
                    Description = "AI 生成的响应文本"
                },
                new()
                {
                    FieldName = "usage",
                    FieldType = FieldType.Object,
                    IsRequired = true,
                    Description = "Token 使用统计"
                }
            },
            SupportsStreaming = true,
            Icon = "robot",
            Color = "#13c2c2"
        };
    }

    private static QueryNodeDefineCommandResponse GetConditionNodeDefine()
    {
        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.Condition,
            NodeTypeName = "条件节点",
            Description = "根据条件表达式评估结果路由到不同分支",
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "condition",
                    FieldType = FieldType.String,
                    IsRequired = true,
                    Description = "条件表达式（JavaScript 表达式）"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "result",
                    FieldType = FieldType.Boolean,
                    IsRequired = true,
                    Description = "条件评估结果（true/false）"
                }
            },
            SupportsStreaming = false,
            Icon = "branches",
            Color = "#fa8c16"
        };
    }

    private static QueryNodeDefineCommandResponse GetForEachNodeDefine()
    {
        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.ForEach,
            NodeTypeName = "循环节点",
            Description = "迭代集合并为每个项目执行循环体",
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "collection",
                    FieldType = FieldType.Array,
                    IsRequired = true,
                    Description = "要迭代的集合"
                },
                new()
                {
                    FieldName = "itemVariable",
                    FieldType = FieldType.String,
                    IsRequired = false,
                    Description = "循环变量名（默认 'item'）"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "results",
                    FieldType = FieldType.Array,
                    IsRequired = true,
                    Description = "所有迭代的结果集合"
                }
            },
            SupportsStreaming = false,
            Icon = "reload",
            Color = "#eb2f96"
        };
    }

    private static QueryNodeDefineCommandResponse GetForkNodeDefine()
    {
        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.Fork,
            NodeTypeName = "分支节点",
            Description = "并行执行多个分支并等待所有分支完成",
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "branches",
                    FieldType = FieldType.Array,
                    IsRequired = true,
                    Description = "分支配置列表"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "results",
                    FieldType = FieldType.Object,
                    IsRequired = true,
                    Description = "所有分支的结果（按分支名称索引）"
                }
            },
            SupportsStreaming = false,
            Icon = "fork",
            Color = "#2f54eb"
        };
    }

    private static QueryNodeDefineCommandResponse GetJavaScriptNodeDefine()
    {
        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.JavaScript,
            NodeTypeName = "JavaScript 节点",
            Description = "执行 JavaScript 代码并可访问工作流上下文",
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "code",
                    FieldType = FieldType.String,
                    IsRequired = true,
                    Description = "JavaScript 代码"
                },
                new()
                {
                    FieldName = "timeout",
                    FieldType = FieldType.Number,
                    IsRequired = false,
                    Description = "执行超时时间（毫秒，默认 5000）"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "result",
                    FieldType = FieldType.Dynamic,
                    IsRequired = true,
                    Description = "代码执行结果"
                }
            },
            SupportsStreaming = false,
            Icon = "code",
            Color = "#faad14"
        };
    }

    private static QueryNodeDefineCommandResponse GetDataProcessNodeDefine()
    {
        return new QueryNodeDefineCommandResponse
        {
            NodeType = NodeType.DataProcess,
            NodeTypeName = "数据处理节点",
            Description = "对输入数据执行转换操作（map、filter、aggregate）",
            InputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "data",
                    FieldType = FieldType.Dynamic,
                    IsRequired = true,
                    Description = "输入数据"
                },
                new()
                {
                    FieldName = "operation",
                    FieldType = FieldType.String,
                    IsRequired = true,
                    Description = "操作类型（map/filter/aggregate）"
                },
                new()
                {
                    FieldName = "expression",
                    FieldType = FieldType.String,
                    IsRequired = true,
                    Description = "处理表达式"
                }
            },
            OutputFields = new List<FieldDefine>
            {
                new()
                {
                    FieldName = "result",
                    FieldType = FieldType.Dynamic,
                    IsRequired = true,
                    Description = "处理后的数据"
                }
            },
            SupportsStreaming = false,
            Icon = "filter",
            Color = "#52c41a"
        };
    }
}
