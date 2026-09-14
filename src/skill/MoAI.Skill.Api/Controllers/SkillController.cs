using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Skill.Commands;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Controllers;

/// <summary>
/// 技能管理接口（管理端点仅平台管理员；options 供应用配置页选择挂载）.
/// </summary>
[ApiController]
[Route("/skill")]
public class SkillController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public SkillController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 分页查询技能列表，仅平台管理员.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QuerySkillsCommandResponse> QuerySkills([FromQuery] QuerySkillsCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询技能详情，仅平台管理员.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public async Task<QuerySkillCommandResponse> QuerySkill(Guid id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QuerySkillCommand { SkillId = id }, ct);
    }

    /// <summary>
    /// 查询可挂载的技能选项（启用中的技能），登录用户可调用.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillOptionsCommandResponse"/>.</returns>
    [HttpGet("options")]
    public Task<QuerySkillOptionsCommandResponse> QueryOptions(CancellationToken ct)
    {
        return _mediator.Send(new QuerySkillOptionsCommand(), ct);
    }

    /// <summary>
    /// 创建技能，仅平台管理员.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回技能 id <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public async Task<SimpleGuid> CreateSkill([FromBody] CreateSkillCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新技能（标识不可修改），仅平台管理员.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateSkill(Guid id, [FromBody] UpdateSkillCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        var cmd = new UpdateSkillCommand
        {
            SkillId = id,
            Name = req.Name,
            Description = req.Description,
            Instructions = req.Instructions,
            Files = req.Files,
        };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除技能（软删除；系统内置技能不可删除），仅平台管理员.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> DeleteSkill(Guid id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new DeleteSkillCommand { SkillId = id }, ct);
    }

    /// <summary>
    /// 启用/禁用技能，仅平台管理员.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/disable")]
    public async Task<EmptyCommandResponse> SetDisable(Guid id, [FromBody] SetSkillDisableRequest req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new SetSkillDisableCommand { SkillId = id, IsDisable = req.IsDisable }, ct);
    }

    /// <summary>
    /// 预上传技能包文件，仅平台管理员.
    /// </summary>
    /// <param name="req">预上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadSkillFileCommandResponse"/>.</returns>
    [HttpPost("file/preupload")]
    public async Task<PreUploadSkillFileCommandResponse> PreUploadFile([FromBody] PreUploadSkillFileCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 完成技能包文件上传，仅平台管理员.
    /// </summary>
    /// <param name="req">完成请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("file/complete")]
    public async Task<EmptyCommandResponse> CompleteFile([FromBody] CompleteSkillFileCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理技能.") { StatusCode = 403 };
        }
    }
}

/// <summary>
/// 启用/禁用技能请求体.
/// </summary>
public class SetSkillDisableRequest
{
    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }
}
