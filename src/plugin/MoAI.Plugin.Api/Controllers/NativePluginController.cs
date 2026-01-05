using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.NativePlugins.Commands;
using MoAI.Plugin.NativePlugins.Models;
using MoAI.Plugin.NativePlugins.Queries;
using MoAI.Plugin.NativePlugins.Queries.Responses;
using MoAI.Plugin.Templates.Commands;

namespace MoAI.Plugin.Controllers;

/// <summary>
/// 内置插件控制器.
/// </summary>
[ApiController]
[Route("/admin/native_plugin")]
[EndpointGroupName("plugin")]
public partial class NativePluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativePluginController"/> class.
    /// 初始化 <see cref="NativePluginController"/> 实例.
    /// </summary>
    /// <param name="mediator">MediatR mediator 实例.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public NativePluginController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 创建内置插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="CreateNativePluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleInt"/>.</returns>
    [HttpPost("create")]
    public async Task<SimpleInt> Create([FromBody] CreateNativePluginCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除内置插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="DeletePluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("delete")]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeletePluginCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询内置插件简要信息列表.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryNativePluginListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryNativePluginListCommandResponse"/>.</returns>
    [HttpPost("list")]
    public async Task<QueryNativePluginListCommandResponse> QueryBaseList([FromBody] QueryNativePluginListCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询内置插件详细信息.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryNativePluginDetailCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="NativePluginDetail"/>.</returns>
    [HttpPost("detail")]
    public async Task<NativePluginDetail> QueryDetail([FromBody] QueryNativePluginDetailCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询模板列表.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryNativePluginTemplateListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryNativePluginTemplateListCommandResponse"/>.</returns>
    [HttpPost("template_list")]
    public async Task<QueryNativePluginTemplateListCommandResponse> QueryTemplateList([FromBody] QueryNativePluginTemplateListCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询模板参数列表.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryNativePluginTemplateParamsCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryNativePluginTemplateParamsCommandResponse"/>.</returns>
    [HttpPost("template_params")]
    public async Task<QueryNativePluginTemplateParamsCommandResponse> QueryTemplateParams([FromBody] QueryNativePluginTemplateParamsCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 运行插件测试（调试）.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="RunTestNativePluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="RunTestNativePluginCommandResponse"/>.</returns>
    [HttpPost("run_test")]
    public async Task<RunTestNativePluginCommandResponse> RunTest([FromBody] RunTestNativePluginCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新原生插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="UpdateNativePluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update")]
    public async Task<EmptyCommandResponse> Update([FromBody] UpdateNativePluginCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新工具类插件.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("update_tool")]
    public async Task<EmptyCommandResponse> Update([FromBody] UseToolNativeCommand req, CancellationToken ct = default)
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