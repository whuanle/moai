//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using MoAI.Database;
//using MoAI.Workflow.Queries;
//using MoAI.Workflow.Queries.Responses;

//namespace MoAI.Workflow.Core.Queries;

///// <summary>
///// <inheritdoc cref="QueryWorkflowDefinitionListCommand"/>
///// </summary>
//public class QueryWorkflowDefinitionListCommandHandler : IRequestHandler<QueryWorkflowDefinitionListCommand, QueryWorkflowDefinitionListCommandResponse>
//{
//    private readonly DatabaseContext _databaseContext;

//    /// <summary>
//    /// Initializes a new instance of the <see cref="QueryWorkflowDefinitionListCommandHandler"/> class.
//    /// </summary>
//    /// <param name="databaseContext">数据库上下文.</param>
//    public QueryWorkflowDefinitionListCommandHandler(DatabaseContext databaseContext)
//    {
//        _databaseContext = databaseContext;
//    }

//    /// <inheritdoc/>
//    public async Task<QueryWorkflowDefinitionListCommandResponse> Handle(QueryWorkflowDefinitionListCommand request, CancellationToken cancellationToken)
//    {
//        // 构建查询
//        var query = _databaseContext.AppWorkflowDesigns
//            .Where(w => w.IsDeleted == 0);

//        // 按团队 ID 过滤
//        if (request.TeamId.HasValue)
//        {
//            query = query.Where(w => w.TeamId == request.TeamId.Value);
//        }

//        // 按关键字搜索
//        if (!string.IsNullOrWhiteSpace(request.Keyword))
//        {
//            query = query.Where(w => w.Name.Contains(request.Keyword) || w.Description.Contains(request.Keyword));
//        }

//        // 获取总数
//        var totalCount = await query.CountAsync(cancellationToken);

//        // 分页查询
//        var items = await query
//            .OrderByDescending(w => w.UpdateTime)
//            .Skip((request.PageIndex - 1) * request.PageSize)
//            .Take(request.PageSize)
//            .Select(w => new QueryWorkflowDefinitionListCommandResponseItem
//            {
//                Id = w.Id,
//                TeamId = w.TeamId,
//                Name = w.Name,
//                Description = w.Description,
//                Avatar = w.Avatar,
//                CreateTime = w.CreateTime,
//                UpdateTime = w.UpdateTime,
//                CreateUserId = w.CreateUserId,
//                UpdateUserId = w.UpdateUserId
//            })
//            .ToListAsync(cancellationToken);

//        // 返回响应
//        return new QueryWorkflowDefinitionListCommandResponse
//        {
//            Items = items,
//            TotalCount = totalCount,
//            PageIndex = request.PageIndex,
//            PageSize = request.PageSize
//        };
//    }
//}
