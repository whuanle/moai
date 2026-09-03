using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Classify.Commands;
using MoAI.Classify.Queries;
using MoAI.Classify.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Classify.Controllers;

/// <summary>
/// 分类管理接口（仅管理员）——插件/应用/知识库分类维护.
/// </summary>
[ApiController]
[Route("/classify")]
public class ClassifyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassifyController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public ClassifyController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询分类列表.
    /// </summary>
    /// <param name="type">分类类型：plugin|app|kb，可为空查询全部.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryClassifyListCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryClassifyListCommandResponse> QueryList([FromQuery] string? type, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryClassifyListCommand { Type = type }, ct);
    }

    /// <summary>
    /// 新增分类.
    /// </summary>
    /// <param name="req">新增请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回新增分类 id.</returns>
    [HttpPost]
    public async Task<SimpleInt> Create([FromBody] CreateClassifyCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改分类.
    /// </summary>
    /// <param name="req">修改请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut]
    public async Task<EmptyCommandResponse> Update([FromBody] UpdateClassifyCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除分类.
    /// </summary>
    /// <param name="req">删除请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteClassifyCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理分类") { StatusCode = 403 };
        }
    }
}
