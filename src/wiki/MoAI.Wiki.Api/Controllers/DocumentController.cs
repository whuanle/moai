using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentManager.Handlers;
using MoAI.Wiki.DocumentManager.Queries;
using MoAI.Wiki.Documents.Commands;
using MoAI.Wiki.Documents.Commands.Responses;
using MoAI.Wiki.Documents.Queries;
using MoAI.Wiki.Documents.Queries.Responses;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 文档相关接口控制器, 提供文档上传、下载、查询、向量化及任务管理等功能.
/// </summary>
[ApiController]
[Route("/wiki/document")]
[Authorize]
public partial class DocumentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentController"/> class.
    /// Initializes a new instance of the <see cref="DocumentController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 的实例.</param>
    /// <param name="userContext">当前请求的用户上下文.</param>
    public DocumentController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 取消文档向量化任务.
    /// </summary>
    /// <param name="req">取消任务的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("create_document")]
    public async Task<EmptyCommandResponse> CancalWikiDocumentTask([FromBody] CancalWikiDocumentTaskCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 清空知识库文档向量.
    /// </summary>
    /// <param name="req">清空向量的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("clear_document")]
    public async Task<EmptyCommandResponse> ClearWikiDocumentEmbedding([FromBody] ClearWikiDocumentEmbeddingCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiRoot)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 完成上传知识库文档上传.
    /// </summary>
    /// <param name="req">完成上传的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("complete_upload_document")]
    public async Task<EmptyCommandResponse> ComplateUploadWikiDocument([FromBody] ComplateUploadWikiDocumentCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除知识库文档.
    /// </summary>
    /// <param name="req">删除文档的命令对象, 包含要删除的 WikiId 与 DocumentId.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("delete_document")]
    public async Task<EmptyCommandResponse> DeleteWikiDocument([FromBody] DeleteWikiDocumentCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取文档下载地址.
    /// </summary>
    /// <param name="req">获取下载地址的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleString"/> 包含下载地址.</returns>
    [HttpPost("download_document")]
    public async Task<SimpleString> DownloadWikiDocument([FromBody] DownloadWikiDocumentCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

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
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预上传文档.
    /// </summary>
    /// <param name="req">预上传文档的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreloadWikiDocumentResponse"/> 包含预上传结果.</returns>
    [HttpPost("preupload_document")]
    public async Task<PreloadWikiDocumentResponse> PreUploadWikiDocument([FromBody] PreUploadWikiDocumentCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询文档信息.
    /// </summary>
    /// <param name="req">查询文档信息的命令对象, 包含 WikiId 与 DocumentId.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentListItem"/> 包含文档信息.</returns>
    [HttpPost("document_info")]
    public async Task<QueryWikiDocumentInfoCommandResponse> QueryWikiDocumentInfo([FromBody] QueryWikiDocumentInfoCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询知识库文档列表.
    /// </summary>
    /// <param name="req">查询文档列表的命令对象, 包含 WikiId 与分页信息.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentListCommandResponse"/> 包含文档列表与元信息.</returns>
    [HttpPost("list")]
    public async Task<QueryWikiDocumentListCommandResponse> QueryWikiDocumentList([FromBody] QueryWikiDocumentListCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询文档任务列表.
    /// </summary>
    /// <param name="req">查询任务列表的命令对象, 包含 WikiId 与筛选信息.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="IReadOnlyCollection{WikiDocumentTaskItem}"/> 包含任务项集合.</returns>
    [HttpPost("task_list")]
    public async Task<IReadOnlyCollection<WikiDocumentTaskItem>> QueryWikiDocumentTaskList([FromBody] QueryWikiDocumentTaskListCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 搜索知识库文本.
    /// </summary>
    /// <param name="req">搜索命令对象, 包含 WikiId 与搜索词.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SearchWikiDocumentTextCommandResponse"/> 包含搜索结果.</returns>
    [HttpPost("search")]
    public async Task<SearchWikiDocumentTextCommandResponse> SearchWikiDocumentText([FromBody] SearchWikiDocumentTextCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改文档名称.
    /// </summary>
    /// <param name="req">修改文档名称的命令对象, 包含 WikiId 与 DocumentId 与新名称.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("update_document_file_name")]
    public async Task<EmptyCommandResponse> UpdateWikiDocumentFileName([FromBody] UpdateWikiDocumentFileNameCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}