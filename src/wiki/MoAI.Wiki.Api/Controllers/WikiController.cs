using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 知识库接口.
/// </summary>
[ApiController]
[Route("/wiki")]
public class WikiController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    public WikiController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 创建知识库，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回知识库 <see cref="SimpleLong"/>.</returns>
    [HttpPost]
    public Task<SimpleLong> CreateWiki([FromBody] CreateWikiCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队下的知识库列表，仅团队成员可访问.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikisCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryWikisCommandResponse> QueryWikis([FromQuery] long teamId, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikisCommand { TeamId = teamId }, ct);
    }

    /// <summary>
    /// 查询知识库上传文件大小上限（MB），任意登录用户可访问，供前端上传前预检.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiUploadLimitCommandResponse"/>.</returns>
    [HttpGet("upload-limit")]
    public Task<QueryWikiUploadLimitCommandResponse> QueryWikiUploadLimit(CancellationToken ct)
    {
        return _mediator.Send(new QueryWikiUploadLimitCommand(), ct);
    }

    /// <summary>
    /// 查询知识库详情，仅团队成员可访问.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public Task<QueryWikiCommandResponse> QueryWiki(long id, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikiCommand { WikiId = id }, ct);
    }

    /// <summary>
    /// 更新知识库，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateWiki(long id, [FromBody] UpdateWikiCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWikiCommand { WikiId = id, Name = req.Name, Description = req.Description, IsPublic = req.IsPublic };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除知识库，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> DeleteWiki(long id, CancellationToken ct)
    {
        return _mediator.Send(new DeleteWikiCommand { WikiId = id }, ct);
    }

    /// <summary>
    /// 设置知识库头像，仅团队 Admin 及以上可操作.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="req">头像请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id}/avatar")]
    public Task<EmptyCommandResponse> UpdateWikiAvatar(long id, [FromBody] UpdateWikiAvatarCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWikiAvatarCommand { WikiId = id, ObjectKey = req.ObjectKey };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询团队可用的向量化/对话模型选项（公开模型 + 已授权模型），仅团队成员可访问.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiModelOptionsCommandResponse"/>.</returns>
    [HttpGet("model-options")]
    public Task<QueryWikiModelOptionsCommandResponse> QueryWikiModelOptions([FromQuery] int teamId, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikiModelOptionsCommand { TeamId = teamId }, ct);
    }

    /// <summary>
    /// 更新知识库向量化配置（向量化模型 + 维度，上限 2000），仅团队 Admin 及以上可操作.
    /// 一旦 wiki 上已有文档被向量化（IsLock=true），该接口将返回 409.
    /// 重排序模型不受该限制，见 <see cref="UpdateWikiRerankModel"/>.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="req">向量化配置请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/embedding-config")]
    public Task<EmptyCommandResponse> UpdateWikiEmbedding(long id, [FromBody] UpdateWikiEmbeddingCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWikiEmbeddingCommand
        {
            WikiId = id,
            EmbeddingModelId = req.EmbeddingModelId,
            EmbeddingDimensions = req.EmbeddingDimensions,
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 更新知识库默认工作流配置（切割 / 元数据生成 / 向量化三步预设），仅团队 Admin 及以上可操作.
    /// 整体覆盖保存：某步骤传 null 表示清除该步骤预设；仅保存预设不触发文档处理.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="req">默认工作流配置请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/workflow-config")]
    public Task<EmptyCommandResponse> UpdateWikiWorkflowConfig(long id, [FromBody] UpdateWikiWorkflowCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWikiWorkflowCommand
        {
            WikiId = id,
            Partition = req.Partition,
            Metadata = req.Metadata,
            Embedding = req.Embedding,
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 更新知识库重排序模型配置，仅团队 Admin 及以上可操作.
    /// 重排序模型可选：传 null（或不传）表示不使用重排序.
    /// 与向量化配置解耦：知识库锁定（IsLock=true）后仍可绑定、更换或解绑.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="req">重排序模型请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/rerank-model")]
    public Task<EmptyCommandResponse> UpdateWikiRerankModel(long id, [FromBody] UpdateWikiRerankModelCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWikiRerankModelCommand
        {
            WikiId = id,
            RerankModelId = req.RerankModelId,
        };
        return _mediator.Send(cmd, ct);
    }
}
