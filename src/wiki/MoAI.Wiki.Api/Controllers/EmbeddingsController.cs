using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.DocumentEmbeddings.Queries;
using MoAI.Wiki.Documents.Queries;
using MoAI.Wiki.Documents.Queries.Responses;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Embedding.Models;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 文档分片相关接口控制器.
/// </summary>
[ApiController]
[Route(ApiPrefix.Document)]
public class EmbeddingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingsController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者接口.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public EmbeddingsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取切割文档分片信息.
    /// </summary>
    /// <param name="req">查询分片的命令对象，包含 WikiId 和 DocumentId 等参数.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentChunksCommandResponse"/>，包含分片信息.</returns>
    [HttpPost("get_chunks")]
    public async Task<QueryWikiDocumentChunksCommandResponse> GetChunks([FromBody] QueryWikiDocumentChunksCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 对需要更新的切片块进行更新.
    /// </summary>
    /// <param name="req">文档块.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="WikiDocumentTextPartitionPreviewCommandResponse"/>，包含预览结果.</returns>
    [HttpPost("update_chnuks")]
    public async Task<EmptyCommandResponse> UpdatePartitionDocument([FromBody] UpdateWikiDocumentChunksCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 调整所有切片的顺序.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("update_chunks_order")]
    public async Task<EmptyCommandResponse> UpdateChunksOrder([FromBody] UpdateWikiDocumentChunksOrderCommand req, CancellationToken ct = default)
    {
        if (req.Chunks.Select(x => x.Order).Distinct().Count() != req.Chunks.Count)
        {
            throw new BusinessException("文本块排序重复.") { StatusCode = 400 };
        }

        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除文档的一个块.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("delete_chunk")]
    public async Task<EmptyCommandResponse> DeleteChunk([FromBody] DeleteWikiDocumentChunkCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 增加文本块.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("add_chunk")]
    public async Task<EmptyCommandResponse> AddChunk([FromBody] AddWikiDocumentChunksCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预览文档切割结果，根据请求参数返回分片预览数据.
    /// </summary>
    /// <param name="req">预览切割的命令对象，包含 WikiId、DocumentId、MaxTokensPerChunk、Overlap 等参数.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="WikiDocumentTextPartitionPreviewCommandResponse"/>，包含预览结果.</returns>
    [HttpPost("text_partition_document")]
    public async Task<WikiDocumentTextPartitionPreviewCommandResponse> PreviewPartitionDocument([FromBody] WikiDocumentTextPartitionPreviewCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// AI 智能切割文档，如果文档的文本内容太多超过 ai 最大输入 token，会失败.
    /// </summary>
    /// <param name="req">查询分片的命令对象，包含 WikiId 和 DocumentId 等参数.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentChunksCommandResponse"/>，包含分片信息.</returns>
    [HttpPost("ai_text_partition_document")]
    public async Task<EmptyCommandResponse> AiPreviewPartitionDocument([FromBody] WikiDocumentAiTextPartionCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 使用 Ai 对文本块进行策略处理.
    /// </summary>
    /// <param name="req">.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="WikiDocumentTextPartitionPreviewCommandResponse"/>，包含预览结果.</returns>
    [HttpPost("ai_generation_chunk")]
    public async Task<WikiDocumentChunkAiGenerationCommandResponse> UpdatePartitionDocument([FromBody] WikiDocumentChunkAiGenerationCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量增加衍生内容.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("add_chunk_derivatives")]
    public async Task<EmptyCommandResponse> AddChunkDerivatives([FromBody] AddWikiDocumentChunkDerivativeCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 向量化文档.
    /// </summary>
    /// <param name="req">向量化文档的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("embedding_document")]
    public async Task<EmptyCommandResponse> Embeddingocument([FromBody] EmbeddingDocumentCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 取消文档向量化任务.
    /// </summary>
    /// <param name="req">取消任务的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("cancal_embedding")]
    public async Task<EmptyCommandResponse> CancalWikiDocumentTask([FromBody] CancalWikiDocumentTaskCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 清空知识库文档向量.
    /// </summary>
    /// <param name="req">清空向量的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("clear_embeddingt")]
    public async Task<EmptyCommandResponse> ClearWikiDocumentEmbedding([FromBody] ClearWikiDocumentEmbeddingCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询文档的向量化任务列表.
    /// </summary>
    /// <param name="req">查询任务列表的命令对象, 包含 WikiId 与筛选信息.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="IReadOnlyCollection{WikiDocumentTaskItem}"/> 包含任务项集合.</returns>
    [HttpPost("task_list")]
    public async Task<IReadOnlyCollection<WikiDocumentEmbeddingTaskItem>> QueryWikiDocumentTaskList([FromBody] QueryWikiDocumentTaskListCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }


    private async Task CheckUserIsMemberAsync(int wikiId, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(
            new QueryUserIsWikiUserCommand
            {
                ContextUserId = _userContext.UserId,
                WikiId = wikiId
            },
            ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}