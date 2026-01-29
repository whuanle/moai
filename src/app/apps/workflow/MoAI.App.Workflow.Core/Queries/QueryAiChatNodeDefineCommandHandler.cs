using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryAiChatNodeDefineCommand"/>
/// </summary>
public class QueryAiChatNodeDefineCommandHandler : IRequestHandler<QueryAiChatNodeDefineCommand, IReadOnlyCollection<NodeDefineItem>>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiChatNodeDefineCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryAiChatNodeDefineCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<NodeDefineItem>> Handle(QueryAiChatNodeDefineCommand request, CancellationToken cancellationToken)
    {
        var modelIds = request.ModelIds.ToList();
        var nodes = new List<NodeDefineItem>();

        // 批量查询 AI 模型信息
        var models = await _databaseContext.AiModels
            .Where(m => modelIds.Contains(m.Id) && m.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        foreach (var model in models)
        {
            nodes.Add(new NodeDefineItem
            {
                NodeType = NodeType.AiChat,
                NodeTypeName = "AI 对话节点",
                Description = "调用 AI 模型进行对话生成",
                ModelId = model.Id,
                ModelName = model.Name,
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
            });
        }

        return nodes;
    }
}
