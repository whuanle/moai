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
/// 技能接口：个人技能仅归属人管理，团队技能仅团队 Admin 及以上可管理，上架市场后所有人可见；
/// list（全量分页）仅平台管理员，其余目标保护在 Handler 校验.
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
    /// 分页查询全量技能列表，仅平台管理员.
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
    /// 查询我的个人技能列表.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillListCommandResponse"/>.</returns>
    [HttpGet("my_list")]
    public Task<QuerySkillListCommandResponse> QueryMyList([FromQuery] QueryMySkillListCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队技能列表，仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillListCommandResponse"/>.</returns>
    [HttpGet("team_list")]
    public Task<QuerySkillListCommandResponse> QueryTeamList([FromQuery] QueryTeamSkillListCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询技能市场列表（已上架公开技能），所有登录用户可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillListCommandResponse"/>.</returns>
    [HttpGet("market_list")]
    public Task<QuerySkillListCommandResponse> QueryMarketList([FromQuery] QuerySkillMarketListCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询技能详情；可见即可查看：系统内置/已公开/所在团队/本人个人技能，平台管理员放行.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public Task<QuerySkillCommandResponse> QuerySkill(Guid id, CancellationToken ct)
    {
        var cmd = new QuerySkillCommand { SkillId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询技能包文件下载地址（预签名，1 小时有效）；可见即可下载，系统内置技能不支持.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillFileDownloadResponse"/>.</returns>
    [HttpGet("{id}/download")]
    public Task<QuerySkillFileDownloadResponse> QueryFileDownloadUrls(Guid id, CancellationToken ct)
    {
        var cmd = new QuerySkillFileDownloadCommand { SkillId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询当前用户可见的技能选项（系统内置 ∪ 公开 ∪ 指定团队 ∪ 本人个人技能）.
    /// </summary>
    /// <param name="teamId">团队 id，0=不限定团队范围.</param>
    /// <param name="includePersonal">是否包含本人个人技能.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySkillOptionsCommandResponse"/>.</returns>
    [HttpGet("options")]
    public Task<QuerySkillOptionsCommandResponse> QueryOptions([FromQuery] int teamId = 0, [FromQuery] bool includePersonal = false, CancellationToken ct = default)
    {
        var cmd = new QuerySkillOptionsCommand { TeamId = teamId, IncludePersonal = includePersonal };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 创建技能：TeamId=0 创建个人技能，大于 0 创建团队技能（需团队管理员）.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回技能 id <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public async Task<SimpleGuid> CreateSkill([FromBody] CreateSkillCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新技能（标识不可修改）：个人技能归属人、团队技能团队管理员或平台管理员.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateSkill(Guid id, [FromBody] UpdateSkillCommand req, CancellationToken ct)
    {
        var cmd = new UpdateSkillCommand
        {
            SkillId = id,
            Name = req.Name,
            Description = req.Description,
            Instructions = req.Instructions,
            Files = req.Files,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除技能（软删除；系统内置技能不可删除）：个人技能归属人、团队技能团队管理员或平台管理员.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> DeleteSkill(Guid id, CancellationToken ct)
    {
        var cmd = new DeleteSkillCommand { SkillId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 启用/禁用技能：个人技能归属人、团队技能团队管理员或平台管理员.
    /// </summary>
    /// <param name="id">技能 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/disable")]
    public async Task<EmptyCommandResponse> SetDisable(Guid id, [FromBody] SetSkillDisableRequest req, CancellationToken ct)
    {
        var cmd = new SetSkillDisableCommand { SkillId = id, IsDisable = req.IsDisable };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 预上传技能包文件，登录用户可调用.
    /// </summary>
    /// <param name="req">预上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadSkillFileCommandResponse"/>.</returns>
    [HttpPost("file/preupload")]
    public async Task<PreUploadSkillFileCommandResponse> PreUploadFile([FromBody] PreUploadSkillFileCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 完成技能包文件上传，登录用户可调用.
    /// </summary>
    /// <param name="req">完成请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("file/complete")]
    public async Task<EmptyCommandResponse> CompleteFile([FromBody] CompleteSkillFileCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以查询全量技能列表.") { StatusCode = 403 };
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
