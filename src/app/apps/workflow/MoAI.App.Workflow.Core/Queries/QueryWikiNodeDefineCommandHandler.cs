using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiNodeDefineCommand"/>
/// </summary>
public class QueryWikiNodeDefineCommandHandler : IRequestHandler<QueryWikiNodeDefineCommand, IReadOnlyCollection<NodeDefineItem>>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiNodeDefineCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryWikiNodeDefineCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<NodeDefineItem>> Handle(QueryWikiNodeDefineCommand request, CancellationToken cancellationToken)
    {
        var wikiIds = request.WikiIds.ToList();
        var nodes = new List<NodeDefineItem>();

        // 批量查询知识库信息
        var wikis = await _databaseContext.Wikis
            .Where(w => wikiIds.Contains(w.Id) && w.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        foreach (var wiki in wikis)
        {
            nodes.Add(new NodeDefineItem
            {
                NodeType = NodeType.Wiki,
                NodeTypeName = "知识库节点",
                Description = "查询知识库并返回相关文档",
                WikiId = wiki.Id,
                WikiName = wiki.Name,
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
            });
        }

        return nodes;
    }
}
