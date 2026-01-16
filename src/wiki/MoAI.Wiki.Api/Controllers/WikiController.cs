using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 知识库相关接口控制器, 提供知识库的创建、删除、成员管理与查询等功能.
/// </summary>
[ApiController]
[Route("/wiki")]
[EndpointGroupName("wiki")]
public partial class WikiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiController"/> class.
    /// 初始化一个 <see cref="WikiController"/> 的新实例.
    /// </summary>
    /// <param name="mediator">MediatR 的实例.</param>
    /// <param name="userContext">当前请求的用户上下文.</param>
    public WikiController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 创建知识库.
    /// </summary>
    /// <param name="req">创建知识库的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleInt"/> 包含新知识库 Id.</returns>
    [HttpPost("create")]
    public async Task<SimpleInt> Create([FromBody] CreateWikiCommand req, CancellationToken ct = default)
    {
        req.SetUserId(_userContext.UserId);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取用户有权访问的知识库列表.
    /// </summary>
    /// <param name="req">查询知识库列表的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="IReadOnlyCollection{QueryWikiInfoResponse}"/> 列表.</returns>
    [HttpPost("query_wiki_list")]
    public async Task<IReadOnlyCollection<QueryWikiInfoResponse>> QueryWikiBaseList([FromBody] QueryWikiBaseListCommand req, CancellationToken ct = default)
    {
        req.SetUserId(_userContext.UserId);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队知识库.
    /// </summary>
    /// <param name="req">查询知识库列表的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="IReadOnlyCollection{QueryWikiInfoResponse}"/> 列表.</returns>
    [HttpPost("query_team_wiki_list")]
    public async Task<IReadOnlyCollection<QueryWikiInfoResponse>> QueryWikiBaseList([FromBody] QueryTeamWikiBaseListCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取知识库详细的信息.
    /// </summary>
    /// <param name="req">查询知识库详细信息的命令对象, 包含 WikiId.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiInfoResponse"/> 包含知识库详细信息.</returns>
    [HttpPost("query_wiki_info")]
    public async Task<QueryWikiInfoResponse> QueryWikiInfo([FromBody] QueryWikiDetailInfoCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(
            new QueryUserIsWikiMemberCommand
            {
                ContextUserId = _userContext.UserId,
                WikiId = req.WikiId
            },
            ct);

        if (userIsWikiUser.IsExist == false)
        {
            throw new BusinessException("未找到知识库.") { StatusCode = 404 };
        }

        if (userIsWikiUser.TeamRole > Team.Models.TeamRole.None)
        {
            return await _mediator.Send(req, ct);
        }

        throw new BusinessException("未找到知识库.") { StatusCode = 404 };
    }
}