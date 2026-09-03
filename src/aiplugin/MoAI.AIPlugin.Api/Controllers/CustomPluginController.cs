using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Commands.Responses;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.AIPlugin.Controllers;

/// <summary>
/// 自定义插件管理接口（仅管理员）—— MCP 与 OpenAPI 插件的导入、更新、查询.
/// </summary>
[ApiController]
[Route("/ai/plugin/custom")]
public class CustomPluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPluginController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public CustomPluginController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 导入 mcp 服务.
    /// </summary>
    /// <param name="req">导入请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回新增插件 id.</returns>
    [HttpPost("import_mcp")]
    public async Task<SimpleGuid> ImportMcp([FromBody] ImportMcpServerPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 完成 openapi 文件上传.
    /// </summary>
    /// <param name="req">导入请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回新增插件 id.</returns>
    [HttpPost("import_openapi")]
    public async Task<SimpleGuid> ImportOpenApi([FromBody] ImportOpenApiPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预上传 openapi 文件.
    /// </summary>
    /// <param name="req">预上传请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadOpenApiFilePluginCommandResponse"/>.</returns>
    [HttpPost("pre_upload_openapi")]
    public async Task<PreUploadOpenApiFilePluginCommandResponse> PreUploadOpenApi([FromBody] PreUploadOpenApiFilePluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改 mcp 插件.
    /// </summary>
    /// <param name="req">修改请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_mcp")]
    public async Task<EmptyCommandResponse> UpdateMcp([FromBody] UpdateMcpServerPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改 openapi 插件.
    /// </summary>
    /// <param name="req">修改请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_openapi")]
    public async Task<EmptyCommandResponse> UpdateOpenApi([FromBody] UpdateOpenApiPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 刷新 mcp 服务器的 tool 列表.
    /// </summary>
    /// <param name="req">刷新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("refresh_mcp")]
    public async Task<EmptyCommandResponse> RefreshMcp([FromBody] RefreshMcpServerPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除自定义插件.
    /// </summary>
    /// <param name="req">删除请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteCustomPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询自定义插件简要信息列表.
    /// </summary>
    /// <param name="req">查询请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryCustomPluginBaseListCommandResponse"/>.</returns>
    [HttpPost("plugin_list")]
    public async Task<QueryCustomPluginBaseListCommandResponse> QueryPluginList([FromBody] QueryCustomPluginListCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取插件的详细信息.
    /// </summary>
    /// <param name="req">查询请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryCustomPluginDetailCommandResponse"/>.</returns>
    [HttpPost("plugin_detail")]
    public async Task<QueryCustomPluginDetailCommandResponse> QueryPluginDetail([FromBody] QueryCustomPluginDetailCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 插件的函数列表.
    /// </summary>
    /// <param name="req">查询请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryCustomPluginFunctionsListCommandResponse"/>.</returns>
    [HttpPost("function_list")]
    public async Task<QueryCustomPluginFunctionsListCommandResponse> QueryPluginFunctionsList([FromBody] QueryCustomPluginFunctionsListCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理插件") { StatusCode = 403 };
        }
    }
}
