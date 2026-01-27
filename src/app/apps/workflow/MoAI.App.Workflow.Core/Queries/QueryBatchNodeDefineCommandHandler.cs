using MediatR;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryBatchNodeDefineCommand"/>
/// </summary>
public class QueryBatchNodeDefineCommandHandler : IRequestHandler<QueryBatchNodeDefineCommand, QueryBatchNodeDefineCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryBatchNodeDefineCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者.</param>
    public QueryBatchNodeDefineCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryBatchNodeDefineCommandResponse> Handle(QueryBatchNodeDefineCommand request, CancellationToken cancellationToken)
    {
        var nodeDefines = new List<NodeDefineResponseItem>();
        var errors = new List<NodeDefineErrorItem>();

        // 并行处理所有请求
        var tasks = request.Requests.Select(async req =>
        {
            try
            {
                // 调用单个节点定义查询
                var command = new QueryNodeDefineCommand
                {
                    NodeType = req.NodeType,
                    PluginId = req.PluginId,
                    ModelId = req.ModelId,
                    WikiId = req.WikiId
                };

                var response = await _mediator.Send(command, cancellationToken);

                // 转换为批量响应项
                return (Success: true, Item: new NodeDefineResponseItem
                {
                    RequestId = req.RequestId,
                    NodeType = response.NodeType,
                    NodeTypeName = response.NodeTypeName,
                    Description = response.Description,
                    InputFields = response.InputFields,
                    OutputFields = response.OutputFields,
                    PluginId = response.PluginId,
                    PluginName = response.PluginName,
                    ModelId = response.ModelId,
                    ModelName = response.ModelName,
                    WikiId = response.WikiId,
                    WikiName = response.WikiName,
                    SupportsStreaming = response.SupportsStreaming,
                    Icon = response.Icon,
                    Color = response.Color
                }, Error: (NodeDefineErrorItem?)null);
            }
            catch (Exception ex)
            {
                // 捕获错误，不中断其他请求的处理
                return (Success: false, Item: (NodeDefineResponseItem?)null, Error: new NodeDefineErrorItem
                {
                    RequestId = req.RequestId,
                    NodeType = req.NodeType.ToString(),
                    ErrorMessage = ex.Message,
                    ErrorCode = ex is Infra.Exceptions.BusinessException be ? be.StatusCode.ToString() : "500"
                });
            }
        });

        var results = await Task.WhenAll(tasks);

        // 分离成功和失败的结果
        foreach (var result in results)
        {
            if (result.Success && result.Item != null)
            {
                nodeDefines.Add(result.Item);
            }
            else if (!result.Success && result.Error != null)
            {
                errors.Add(result.Error);
            }
        }

        return new QueryBatchNodeDefineCommandResponse
        {
            NodeDefines = nodeDefines,
            Errors = errors
        };
    }
}
