using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Wiki.Plugins.Paddleocr.Commands;
using MoAI.Wiki.Plugins.Paddleocr.Models;
using MoAI.Wiki.Plugins.Paddleocr.Queries;
using MoAI.Wiki.Plugins.Paddleocr.Queries.Responses;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins;

/// <summary>
/// 飞桨 OCR 插件接口.
/// </summary>
[ApiController]
[Route("/wiki/plugin/paddleocr")]
[EndpointGroupName("wiki")]
public class PaddleocrController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleocrController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContext"></param>
    public PaddleocrController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询可用的飞桨 OCR 插件列表.
    /// </summary>
    /// <param name="req">查询命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>插件列表.</returns>
    [HttpPost("plugin_list")]
    public async Task<QueryPaddleocrPluginListCommandResponse> QueryPluginListAsync([FromBody] QueryPaddleocrPluginListCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预览飞桨 OCR 文档解析结果.
    /// </summary>
    /// <param name="req">预览命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>预览结果.</returns>
    [HttpPost("preview")]
    public async Task<PaddleocrPreviewResult> PreviewAsync([FromBody] PreviewPaddleocrDocumentCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 确认导入飞桨 OCR 解析的文档到知识库.
    /// </summary>
    /// <param name="req">导入命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>文档 id.</returns>
    [HttpPost("import")]
    public async Task<SimpleInt> ImportAsync([FromBody] ImportPaddleocrDocumentCommand req, CancellationToken ct = default)
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
