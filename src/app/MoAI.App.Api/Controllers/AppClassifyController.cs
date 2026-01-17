using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.App.Classify.Commands;
using MoAI.App.Classify.Queries;
using MoAI.App.Classify.Queries.Responses;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Controllers;

/// <summary>
/// 应用分类管理.
/// </summary>
[ApiController]
[Route("/admin/appclassify")]
[EndpointGroupName("app")]
public partial class AppClassifyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppClassifyController"/> class.
    /// </summary>
    /// <param name="mediator">IMediator instance.</param>
    /// <param name="userContext">UserContext instance.</param>
    public AppClassifyController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 新增分类.
    /// </summary>
    /// <param name="req">新增分类命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleInt"/>，包含新建记录的 Id 等信息.</returns>
    [HttpPost("add_classify")]
    public async Task<SimpleInt> Create([FromBody] CreateAppClassifyCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除分类.
    /// </summary>
    /// <param name="req">删除分类命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("delete_classify")]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteAppClassifyCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改分类.
    /// </summary>
    /// <param name="req">修改分类命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_classify")]
    public async Task<EmptyCommandResponse> Update([FromBody] UpdateAppClassifyCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckIsAdminAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);
        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}
