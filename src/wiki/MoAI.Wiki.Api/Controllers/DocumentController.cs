using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Wiki.DocumentManager.Queries;
using MoAI.Wiki.Documents.Handlers;
using MoAI.Wiki.Documents.Models;
using MoAI.Wiki.Documents.Queries;
using MoAI.Wiki.Plugins.OpenApi;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 文档相关接口控制器, 提供文档上传、下载、查询、向量化及任务管理等功能.
/// </summary>
[ApiController]
[Route("/wiki/document")]
[EndpointGroupName("wiki")]
public partial class DocumentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
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
    /// 完成上传知识库文档上传.
    /// </summary>
    /// <param name="req">完成上传的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("complete_upload_document")]
    public async Task<EmptyCommandResponse> ComplateUploadWikiDocument([FromBody] ComplateUploadWikiDocumentCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

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
        await CheckUserIsMemberAsync(req.WikiId, ct);

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
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预上传文档.
    /// </summary>
    /// <param name="req">预上传文档的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadWikiDocumentCommandResponse"/> 包含预上传结果.</returns>
    [HttpPost("preupload_document")]
    public async Task<PreUploadWikiDocumentCommandResponse> PreUploadWikiDocument([FromBody] PreUploadWikiDocumentCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

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
        await CheckUserIsMemberAsync(req.WikiId, ct);

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
        await CheckUserIsMemberAsync(req.WikiId, ct);

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
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckUserIsMemberAsync(int wikiId, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(
            new QueryUserIsWikiMemberCommand
            {
                ContextUserId = _userContext.UserId,
                WikiId = wikiId
            },
            ct);

        if (userIsWikiUser.TeamRole == TeamRole.None)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}