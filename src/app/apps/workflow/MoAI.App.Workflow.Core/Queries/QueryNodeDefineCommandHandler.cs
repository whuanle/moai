using MediatR;
using MoAI.Infra.Exceptions;
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
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNodeDefineCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者.</param>
    public QueryNodeDefineCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryNodeDefineCommandResponse> Handle(QueryNodeDefineCommand request, CancellationToken cancellationToken)
    {
        var allNodes = new List<NodeDefineItem>();

        // 遍历每个节点类型及其实例标识
        foreach (var node in request.Nodes)
        {
            var nodeType = node.Key;
            var instanceIds = node.Value;

            switch (nodeType)
            {
                case NodeType.Start:
                    allNodes.Add(GetStartNodeDefine());
                    break;

                case NodeType.End:
                    allNodes.Add(GetEndNodeDefine());
                    break;

                case NodeType.Plugin:
                    var pluginIds = instanceIds.Select(int.Parse).ToList();
                    var pluginResponse = await _mediator.Send(new QueryPluginNodeDefineCommand { PluginIds = pluginIds }, cancellationToken);
                    allNodes.AddRange(pluginResponse);
                    break;

                case NodeType.Wiki:
                    var wikiIds = instanceIds.Select(int.Parse).ToList();
                    var wikiResponse = await _mediator.Send(new QueryWikiNodeDefineCommand { WikiIds = wikiIds }, cancellationToken);
                    allNodes.AddRange(wikiResponse);
                    break;

                case NodeType.AiChat:
                    var modelIds = instanceIds.Select(int.Parse).ToList();
                    var aiChatResponse = await _mediator.Send(new QueryAiChatNodeDefineCommand { ModelIds = modelIds }, cancellationToken);
                    allNodes.AddRange(aiChatResponse);
                    break;

                case NodeType.Condition:
                    allNodes.Add(GetConditionNodeDefine());
                    break;

                case NodeType.ForEach:
                    allNodes.Add(GetForEachNodeDefine());
                    break;

                case NodeType.Fork:
                    allNodes.Add(GetForkNodeDefine());
                    break;

                case NodeType.JavaScript:
                    allNodes.Add(GetJavaScriptNodeDefine());
                    break;

                case NodeType.DataProcess:
                    allNodes.Add(GetDataProcessNodeDefine());
                    break;

                default:
                    throw new BusinessException($"不支持的节点类型: {nodeType}") { StatusCode = 400 };
            }
        }

        return new QueryNodeDefineCommandResponse
        {
            Nodes = allNodes
        };
    }

    private static NodeDefineItem GetStartNodeDefine()
    {
        return new NodeDefineItem
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

    private static NodeDefineItem GetEndNodeDefine()
    {
        return new NodeDefineItem
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

    private static NodeDefineItem GetConditionNodeDefine()
    {
        return new NodeDefineItem
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

    private static NodeDefineItem GetForEachNodeDefine()
    {
        return new NodeDefineItem
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

    private static NodeDefineItem GetForkNodeDefine()
    {
        return new NodeDefineItem
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

    private static NodeDefineItem GetJavaScriptNodeDefine()
    {
        return new NodeDefineItem
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

    private static NodeDefineItem GetDataProcessNodeDefine()
    {
        return new NodeDefineItem
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
