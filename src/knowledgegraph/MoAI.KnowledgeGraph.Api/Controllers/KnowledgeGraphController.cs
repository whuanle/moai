using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Controllers;

/// <summary>
/// 知识图谱接口.
/// </summary>
[ApiController]
[Route("/knowledge-graph")]
public class KnowledgeGraphController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public KnowledgeGraphController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 创建知识图谱.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>图谱 id.</returns>
    [HttpPost]
    public Task<SimpleLong> Create([FromBody] CreateKnowledgeGraphCommand req, CancellationToken ct)
        => _mediator.Send(req, ct);

    /// <summary>
    /// 查询团队下的知识图谱列表.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>列表.</returns>
    [HttpGet("list")]
    public Task<QueryKnowledgeGraphsCommandResponse> List([FromQuery] long teamId, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphsCommand { TeamId = teamId }, ct);

    /// <summary>
    /// 查询模板目录.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>模板列表.</returns>
    [HttpGet("templates")]
    public Task<QueryKnowledgeGraphTemplatesCommandResponse> Templates(CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphTemplatesCommand(), ct);

    /// <summary>
    /// 查询图谱详情.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>详情.</returns>
    [HttpGet("{id}")]
    public Task<QueryKnowledgeGraphCommandResponse> Detail(long id, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphCommand { KnowledgeGraphId = id }, ct);

    /// <summary>
    /// 更新图谱.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}")]
    public Task<EmptyCommandResponse> Update(long id, [FromBody] UpdateKnowledgeGraphCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphCommand { KnowledgeGraphId = id, Name = req.Name, Description = req.Description }, ct);

    /// <summary>
    /// 删除图谱.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> Delete(long id, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphCommand { KnowledgeGraphId = id }, ct);

    /// <summary>
    /// 查询 schema.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>schema.</returns>
    [HttpGet("{id}/schema")]
    public Task<QueryKnowledgeGraphSchemaCommandResponse> Schema(long id, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = id }, ct);

    /// <summary>
    /// 新增实体类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>类型 id.</returns>
    [HttpPost("{id}/entity-types")]
    public Task<SimpleLong> CreateEntityType(long id, [FromBody] CreateKnowledgeGraphEntityTypeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphEntityTypeCommand { KnowledgeGraphId = id, Name = req.Name, Color = req.Color, Description = req.Description }, ct);

    /// <summary>
    /// 修改实体类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">实体类型 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/entity-types/{typeId}")]
    public Task<EmptyCommandResponse> UpdateEntityType(long id, long typeId, [FromBody] UpdateKnowledgeGraphEntityTypeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphEntityTypeCommand { KnowledgeGraphId = id, EntityTypeId = typeId, Name = req.Name, Color = req.Color, Description = req.Description }, ct);

    /// <summary>
    /// 删除实体类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">实体类型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/entity-types/{typeId}")]
    public Task<EmptyCommandResponse> DeleteEntityType(long id, long typeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphEntityTypeCommand { KnowledgeGraphId = id, EntityTypeId = typeId }, ct);

    /// <summary>
    /// 新增关系类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>类型 id.</returns>
    [HttpPost("{id}/relation-types")]
    public Task<SimpleLong> CreateRelationType(long id, [FromBody] CreateKnowledgeGraphRelationTypeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphRelationTypeCommand { KnowledgeGraphId = id, Name = req.Name, Color = req.Color, Description = req.Description, SourceTypeId = req.SourceTypeId, TargetTypeId = req.TargetTypeId }, ct);

    /// <summary>
    /// 修改关系类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">关系类型 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/relation-types/{typeId}")]
    public Task<EmptyCommandResponse> UpdateRelationType(long id, long typeId, [FromBody] UpdateKnowledgeGraphRelationTypeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphRelationTypeCommand { KnowledgeGraphId = id, RelationTypeId = typeId, Name = req.Name, Color = req.Color, Description = req.Description, SourceTypeId = req.SourceTypeId, TargetTypeId = req.TargetTypeId }, ct);

    /// <summary>
    /// 删除关系类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">关系类型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/relation-types/{typeId}")]
    public Task<EmptyCommandResponse> DeleteRelationType(long id, long typeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphRelationTypeCommand { KnowledgeGraphId = id, RelationTypeId = typeId }, ct);

    /// <summary>
    /// 节点分页.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>分页结果.</returns>
    [HttpPost("{id}/nodes/list")]
    public Task<QueryKnowledgeGraphNodesCommandResponse> ListNodes(long id, [FromBody] QueryKnowledgeGraphNodesCommand req, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphNodesCommand { KnowledgeGraphId = id, EntityTypeId = req.EntityTypeId, Keyword = req.Keyword, PageNo = req.PageNo, PageSize = req.PageSize }, ct);

    /// <summary>
    /// 新增节点.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点 id.</returns>
    [HttpPost("{id}/nodes")]
    public Task<SimpleString> CreateNode(long id, [FromBody] CreateKnowledgeGraphNodeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphNodeCommand { KnowledgeGraphId = id, EntityTypeId = req.EntityTypeId, Name = req.Name, Description = req.Description }, ct);

    /// <summary>
    /// 节点详情.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点.</returns>
    [HttpGet("{id}/nodes/{nodeId}")]
    public Task<QueryKnowledgeGraphNodeCommandResponse> NodeDetail(long id, string nodeId, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphNodeCommand { KnowledgeGraphId = id, NodeId = nodeId }, ct);

    /// <summary>
    /// 修改节点.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/nodes/{nodeId}")]
    public Task<EmptyCommandResponse> UpdateNode(long id, string nodeId, [FromBody] UpdateKnowledgeGraphNodeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphNodeCommand { KnowledgeGraphId = id, NodeId = nodeId, EntityTypeId = req.EntityTypeId, Name = req.Name, Description = req.Description }, ct);

    /// <summary>
    /// 删除节点.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/nodes/{nodeId}")]
    public Task<EmptyCommandResponse> DeleteNode(long id, string nodeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphNodeCommand { KnowledgeGraphId = id, NodeId = nodeId }, ct);

    /// <summary>
    /// 边分页.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>分页结果.</returns>
    [HttpPost("{id}/edges/list")]
    public Task<QueryKnowledgeGraphEdgesCommandResponse> ListEdges(long id, [FromBody] QueryKnowledgeGraphEdgesCommand req, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphEdgesCommand { KnowledgeGraphId = id, RelationTypeId = req.RelationTypeId, NodeId = req.NodeId, PageNo = req.PageNo, PageSize = req.PageSize }, ct);

    /// <summary>
    /// 新增边.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>边 id.</returns>
    [HttpPost("{id}/edges")]
    public Task<SimpleString> CreateEdge(long id, [FromBody] CreateKnowledgeGraphEdgeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphEdgeCommand { KnowledgeGraphId = id, RelationTypeId = req.RelationTypeId, SourceNodeId = req.SourceNodeId, TargetNodeId = req.TargetNodeId }, ct);

    /// <summary>
    /// 边详情.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>边.</returns>
    [HttpGet("{id}/edges/{edgeId}")]
    public Task<QueryKnowledgeGraphEdgeCommandResponse> EdgeDetail(long id, string edgeId, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphEdgeCommand { KnowledgeGraphId = id, EdgeId = edgeId }, ct);

    /// <summary>
    /// 修改边.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/edges/{edgeId}")]
    public Task<EmptyCommandResponse> UpdateEdge(long id, string edgeId, [FromBody] UpdateKnowledgeGraphEdgeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphEdgeCommand { KnowledgeGraphId = id, EdgeId = edgeId, RelationTypeId = req.RelationTypeId }, ct);

    /// <summary>
    /// 删除边.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/edges/{edgeId}")]
    public Task<EmptyCommandResponse> DeleteEdge(long id, string edgeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphEdgeCommand { KnowledgeGraphId = id, EdgeId = edgeId }, ct);

    /// <summary>
    /// 画布有界子图查询（仅托管图）.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>子图节点与边.</returns>
    [HttpPost("{id}/canvas")]
    public Task<QueryKnowledgeGraphCanvasCommandResponse> Canvas(long id, [FromBody] QueryKnowledgeGraphCanvasCommand req, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphCanvasCommand { KnowledgeGraphId = id, EntityTypeId = req.EntityTypeId, RelationTypeId = req.RelationTypeId, Keyword = req.Keyword, Limit = req.Limit }, ct);

    /// <summary>
    /// 节点一跳邻接展开（仅托管图）.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="limit">邻居数量上限（0=默认 100）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>邻居节点与相连的边.</returns>
    [HttpGet("{id}/nodes/{nodeId}/neighbors")]
    public Task<QueryKnowledgeGraphCanvasCommandResponse> NodeNeighbors(long id, string nodeId, [FromQuery] int limit, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphNodeNeighborsCommand { KnowledgeGraphId = id, NodeId = nodeId, Limit = limit }, ct);
}
