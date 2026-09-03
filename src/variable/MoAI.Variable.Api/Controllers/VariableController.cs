using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Variable.Commands;
using MoAI.Variable.Commands.Responses;
using MoAI.Variable.Queries;
using MoAI.Variable.Queries.Responses;

namespace MoAI.Variable.Controllers;

/// <summary>
/// 团队变量接口.
/// </summary>
[ApiController]
[Route("/variable")]
public class VariableController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    public VariableController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 创建变量，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回变量 <see cref="SimpleLong"/>.</returns>
    [HttpPost]
    public Task<SimpleLong> CreateVariable([FromBody] CreateVariableCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新变量，需要团队 Admin 及以上角色；私密变量值留空表示保持不变.
    /// </summary>
    /// <param name="id">变量 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateVariable(long id, [FromBody] UpdateVariableCommand req, CancellationToken ct)
    {
        var cmd = new UpdateVariableCommand { VariableId = id, GroupName = req.GroupName, Value = req.Value, Description = req.Description };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除变量，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">变量 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> DeleteVariable(long id, CancellationToken ct)
    {
        return _mediator.Send(new DeleteVariableCommand { VariableId = id }, ct);
    }

    /// <summary>
    /// 查询团队变量列表（私密变量值掩码），仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryVariablesCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryVariablesCommandResponse> QueryVariables([FromQuery] QueryVariablesCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询变量详情；私密变量的值仅 Owner/Admin 可见.
    /// </summary>
    /// <param name="id">变量 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryVariableCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public Task<QueryVariableCommandResponse> QueryVariable(long id, CancellationToken ct)
    {
        return _mediator.Send(new QueryVariableCommand { VariableId = id }, ct);
    }

    /// <summary>
    /// 对文本执行 <c>${key}</c> 变量替换（含私密变量），仅团队 Admin 及以上可调用.
    /// </summary>
    /// <param name="req">替换请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubstituteVariableCommandResponse"/>.</returns>
    [HttpPost("substitute")]
    public Task<SubstituteVariableCommandResponse> Substitute([FromBody] SubstituteVariableCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }
}
