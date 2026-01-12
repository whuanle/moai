using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Wiki.Plugins.OpenApi;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 文档相关接口控制器, 提供文档上传、下载、查询、向量化及任务管理等功能.
/// </summary>
[ApiController]
[Route("/wiki/plugin/openapi")]
[EndpointGroupName("wiki")]
public partial class OpenApiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 的实例.</param>
    /// <param name="userContext">当前请求的用户上下文.</param>
    public OpenApiController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 上传 api 接口文件生成知识库文档.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("import_api")]
    public async Task<EmptyCommandResponse> ImportOpenApiToWiki([FromBody] ImportOpenApiToWikiCommand req, CancellationToken ct = default)
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