using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Classify.Commands;
using MoAI.Plugin.Classify.Queries;
using MoAI.Plugin.Classify.Queries.Responses;

namespace MoAI.Plugin.Controllers;

/// <summary>
/// 插件分类管理.
/// </summary>
[ApiController]
[Route("/admin/classify")]
public partial class ClassifyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassifyController"/> class.
    /// </summary>
    /// <param name="mediator">IMediator instance.</param>
    /// <param name="userContext">UserContext instance.</param>
    public ClassifyController(IMediator mediator, UserContext userContext)
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
    public async Task<SimpleInt> Create([FromBody] CreatePluginClassifyCommand req, CancellationToken ct = default)
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
    public async Task<EmptyCommandResponse> Delete([FromBody] DeletePluginClassifyCommand req, CancellationToken ct = default)
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
    [HttpDelete("update_classify")]
    public async Task<EmptyCommandResponse> Update([FromBody] UpdatePluginClassifyCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取分类列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginClassifyListCommandResponse"/>，包含分类列表数据.</returns>
    [HttpGet("classify_list")]
    public async Task<QueryPluginClassifyListCommandResponse> QueryList(CancellationToken ct = default)
    {
        return await _mediator.Send(new QueryPluginClassifyListCommand(), ct);
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