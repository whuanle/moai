using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Models;
using MoAI.App.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Controllers;

/// <summary>
/// 知识图谱外部开放接口：应用 token（团队级授权）维护实体/关系/模型；认证由 ExternalAuthenticationMiddleware 统一处理.
/// </summary>
[ApiController]
[Route("/external/knowledge-graph")]
[AllowAnonymous]
public class ExternalKnowledgeGraphController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalKnowledgeGraphController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public ExternalKnowledgeGraphController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询 token 归属团队下的知识图谱列表（外部语义：团队级授权，仅返回托管图）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryExternalGraphsResponse"/>.</returns>
    [HttpPost("list")]
    public async Task<QueryExternalGraphsResponse> List(CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalGraphsCommand { Caller = caller }, ct);
    }

    /// <summary>
    /// 查询图谱 schema（实体类型 + 关系类型）（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryExternalGraphSchemaCommandResponse"/>.</returns>
    [HttpGet("{kgId:long}/schema")]
    public async Task<QueryExternalGraphSchemaCommandResponse> Schema(long kgId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalGraphSchemaCommand { Caller = caller, KnowledgeGraphId = kgId }, ct);
    }

    /// <summary>
    /// 节点分页（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryKnowledgeGraphNodesCommandResponse"/>.</returns>
    [HttpPost("{kgId:long}/nodes/list")]
    public async Task<QueryKnowledgeGraphNodesCommandResponse> ListNodes(long kgId, [FromBody] QueryExternalNodesCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalNodesCommand { Caller = caller, KnowledgeGraphId = kgId, EntityTypeId = req.EntityTypeId, Keyword = req.Keyword, PageNo = req.PageNo, PageSize = req.PageSize }, ct);
    }

    /// <summary>
    /// 节点详情（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryKnowledgeGraphNodeCommandResponse"/>.</returns>
    [HttpGet("{kgId:long}/nodes/{nodeId}")]
    public async Task<QueryKnowledgeGraphNodeCommandResponse> NodeDetail(long kgId, string nodeId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalNodeCommand { Caller = caller, KnowledgeGraphId = kgId, NodeId = nodeId }, ct);
    }

    /// <summary>
    /// 节点一跳邻接展开，返回邻居节点与相连的边（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="limit">邻居数量上限（默认 50，取值 1-500）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryKnowledgeGraphCanvasCommandResponse"/>.</returns>
    [HttpGet("{kgId:long}/nodes/{nodeId}/neighbors")]
    public async Task<QueryKnowledgeGraphCanvasCommandResponse> NodeNeighbors(long kgId, string nodeId, [FromQuery] int? limit, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalNodeNeighborsCommand { Caller = caller, KnowledgeGraphId = kgId, NodeId = nodeId, Limit = limit ?? 50 }, ct);
    }

    /// <summary>
    /// 新增节点（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点 id.</returns>
    [HttpPost("{kgId:long}/nodes")]
    public async Task<SimpleString> CreateNode(long kgId, [FromBody] CreateExternalNodeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new CreateExternalNodeCommand { Caller = caller, KnowledgeGraphId = kgId, EntityTypeId = req.EntityTypeId, Name = req.Name, Description = req.Description, Properties = req.Properties }, ct);
    }

    /// <summary>
    /// 修改节点（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{kgId:long}/nodes/{nodeId}")]
    public async Task<EmptyCommandResponse> UpdateNode(long kgId, string nodeId, [FromBody] UpdateExternalNodeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new UpdateExternalNodeCommand { Caller = caller, KnowledgeGraphId = kgId, NodeId = nodeId, EntityTypeId = req.EntityTypeId, Name = req.Name, Description = req.Description, Properties = req.Properties }, ct);
    }

    /// <summary>
    /// 删除节点（连带其边）（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{kgId:long}/nodes/{nodeId}")]
    public async Task<EmptyCommandResponse> DeleteNode(long kgId, string nodeId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new DeleteExternalNodeCommand { Caller = caller, KnowledgeGraphId = kgId, NodeId = nodeId }, ct);
    }

    /// <summary>
    /// 批量新增节点（外部语义：团队级授权，单批次最多 200 条）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalBatchResponse"/>.</returns>
    [HttpPost("{kgId:long}/nodes/batch")]
    public async Task<ExternalBatchResponse> CreateNodesBatch(long kgId, [FromBody] CreateExternalNodesBatchCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new CreateExternalNodesBatchCommand { Caller = caller, KnowledgeGraphId = kgId, Items = req.Items }, ct);
    }

    /// <summary>
    /// 边分页（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryKnowledgeGraphEdgesCommandResponse"/>.</returns>
    [HttpPost("{kgId:long}/edges/list")]
    public async Task<QueryKnowledgeGraphEdgesCommandResponse> ListEdges(long kgId, [FromBody] QueryExternalEdgesCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalEdgesCommand { Caller = caller, KnowledgeGraphId = kgId, RelationTypeId = req.RelationTypeId, NodeId = req.NodeId, PageNo = req.PageNo, PageSize = req.PageSize }, ct);
    }

    /// <summary>
    /// 边详情（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryKnowledgeGraphEdgeCommandResponse"/>.</returns>
    [HttpGet("{kgId:long}/edges/{edgeId}")]
    public async Task<QueryKnowledgeGraphEdgeCommandResponse> EdgeDetail(long kgId, string edgeId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalEdgeCommand { Caller = caller, KnowledgeGraphId = kgId, EdgeId = edgeId }, ct);
    }

    /// <summary>
    /// 新增边（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>边 id.</returns>
    [HttpPost("{kgId:long}/edges")]
    public async Task<SimpleString> CreateEdge(long kgId, [FromBody] CreateExternalEdgeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new CreateExternalEdgeCommand { Caller = caller, KnowledgeGraphId = kgId, RelationTypeId = req.RelationTypeId, SourceNodeId = req.SourceNodeId, TargetNodeId = req.TargetNodeId }, ct);
    }

    /// <summary>
    /// 修改边（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{kgId:long}/edges/{edgeId}")]
    public async Task<EmptyCommandResponse> UpdateEdge(long kgId, string edgeId, [FromBody] UpdateExternalEdgeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new UpdateExternalEdgeCommand { Caller = caller, KnowledgeGraphId = kgId, EdgeId = edgeId, RelationTypeId = req.RelationTypeId }, ct);
    }

    /// <summary>
    /// 删除边（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{kgId:long}/edges/{edgeId}")]
    public async Task<EmptyCommandResponse> DeleteEdge(long kgId, string edgeId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new DeleteExternalEdgeCommand { Caller = caller, KnowledgeGraphId = kgId, EdgeId = edgeId }, ct);
    }

    /// <summary>
    /// 批量新增边（外部语义：团队级授权，单批次最多 200 条）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalBatchResponse"/>.</returns>
    [HttpPost("{kgId:long}/edges/batch")]
    public async Task<ExternalBatchResponse> CreateEdgesBatch(long kgId, [FromBody] CreateExternalEdgesBatchCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new CreateExternalEdgesBatchCommand { Caller = caller, KnowledgeGraphId = kgId, Items = req.Items }, ct);
    }

    /// <summary>
    /// 新增实体类型（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>类型 id.</returns>
    [HttpPost("{kgId:long}/entity-types")]
    public async Task<SimpleLong> CreateEntityType(long kgId, [FromBody] CreateExternalEntityTypeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new CreateExternalEntityTypeCommand { Caller = caller, KnowledgeGraphId = kgId, Name = req.Name, Color = req.Color, Description = req.Description, Properties = req.Properties }, ct);
    }

    /// <summary>
    /// 修改实体类型（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="typeId">实体类型 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{kgId:long}/entity-types/{typeId:long}")]
    public async Task<EmptyCommandResponse> UpdateEntityType(long kgId, long typeId, [FromBody] UpdateExternalEntityTypeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new UpdateExternalEntityTypeCommand { Caller = caller, KnowledgeGraphId = kgId, EntityTypeId = typeId, Name = req.Name, Color = req.Color, Description = req.Description, Properties = req.Properties }, ct);
    }

    /// <summary>
    /// 删除实体类型（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="typeId">实体类型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{kgId:long}/entity-types/{typeId:long}")]
    public async Task<EmptyCommandResponse> DeleteEntityType(long kgId, long typeId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new DeleteExternalEntityTypeCommand { Caller = caller, KnowledgeGraphId = kgId, EntityTypeId = typeId }, ct);
    }

    /// <summary>
    /// 新增关系类型（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>类型 id.</returns>
    [HttpPost("{kgId:long}/relation-types")]
    public async Task<SimpleLong> CreateRelationType(long kgId, [FromBody] CreateExternalRelationTypeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new CreateExternalRelationTypeCommand { Caller = caller, KnowledgeGraphId = kgId, Name = req.Name, Color = req.Color, Description = req.Description, SourceTypeId = req.SourceTypeId, TargetTypeId = req.TargetTypeId }, ct);
    }

    /// <summary>
    /// 修改关系类型（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="typeId">关系类型 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{kgId:long}/relation-types/{typeId:long}")]
    public async Task<EmptyCommandResponse> UpdateRelationType(long kgId, long typeId, [FromBody] UpdateExternalRelationTypeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new UpdateExternalRelationTypeCommand { Caller = caller, KnowledgeGraphId = kgId, RelationTypeId = typeId, Name = req.Name, Color = req.Color, Description = req.Description, SourceTypeId = req.SourceTypeId, TargetTypeId = req.TargetTypeId }, ct);
    }

    /// <summary>
    /// 删除关系类型（外部语义：团队级授权）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="typeId">关系类型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{kgId:long}/relation-types/{typeId:long}")]
    public async Task<EmptyCommandResponse> DeleteRelationType(long kgId, long typeId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new DeleteExternalRelationTypeCommand { Caller = caller, KnowledgeGraphId = kgId, RelationTypeId = typeId }, ct);
    }

    private ExternalGraphCaller RequireCaller()
    {
        // 从 HttpContext.Items 取认证中间件写入的 token 上下文（ExternalJwtBearerAuthenticationHandler 填充）
        var tokenContext = HttpContext.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var value) ? value as ExternalTokenContext : null;
        if (tokenContext == null)
        {
            throw new BusinessException("外部 token 无效.") { StatusCode = 401 };
        }

        if (tokenContext.SubjectType != UserType.ExternalApp)
        {
            throw new BusinessException("该接口仅支持应用 token.") { StatusCode = 403 };
        }

        if (tokenContext.AccessAppId == null)
        {
            throw new BusinessException("外部 token 缺少应用接入标识.") { StatusCode = 401 };
        }

        return new ExternalGraphCaller { TeamId = tokenContext.TeamId, AccessAppId = tokenContext.AccessAppId.Value };
    }
}
