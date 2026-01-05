using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.CustomPlugins.Commands.Responses;
using MoAI.Plugin.CustomPlugins.Queries;
using MoAI.Plugin.TeamPlugins.Commands;
using MoAI.Plugin.TeamPlugins.Queries;
using MoAI.Plugin.TeamPlugins.Queries.Responses;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.Plugin.Controllers;

/// <summary>
/// 团队插件管理.
/// </summary>
[ApiController]
[Route("/team/plugin")]
[EndpointGroupName("plugin")]
public partial class TeamPluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamPluginController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public TeamPluginController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 删除团队插件.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpDelete("delete")]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteTeamPluginCommand req, CancellationToken ct = default)
    {
        await CheckUserIsTeamAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 导入团队 MCP 服务插件.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("import_mcp")]
    public async Task<SimpleInt> ImportMcp([FromBody] ImportTeamMcpServerPluginCommand req, CancellationToken ct = default)
    {
        await CheckUserIsTeamAdminAsync(req.TeamId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 导入团队 OpenAPI 插件.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("import_openapi")]
    public async Task<SimpleInt> ImportOpenApi([FromBody] ImportTeamOpenApiPluginCommand req, CancellationToken ct = default)
    {
        await CheckUserIsTeamAdminAsync(req.TeamId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预上传 openapi 文件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="PreUploadOpenApiFilePluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadOpenApiFilePluginCommandResponse"/>.</returns>
    [HttpPost("pre_upload_openapi")]
    public async Task<PreUploadOpenApiFilePluginCommandResponse> PreUploadOpenApi([FromBody] PreTeamUploadOpenApiFilePluginCommand req, CancellationToken ct = default)
    {
        await CheckUserIsTeamAdminAsync(req.TeamId, ct);

        return await _mediator.Send(
            new PreUploadOpenApiFilePluginCommand
            {
                FileName = req.FileName,
                FileSize = req.FileSize,
                ContentType = req.ContentType,
                MD5 = req.MD5,
                PluginName = req.PluginName
            },
            ct);
    }

    /// <summary>
    /// 查询团队插件列表.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("list")]
    public async Task<QueryTeamPluginListCommandResponse> QueryList([FromBody] QueryTeamPluginListCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Collaborator)
        {
            throw new BusinessException("没有操作权限");
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队可用的插件列表（包含公开插件和团队专属插件）.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("available_list")]
    public async Task<QueryTeamAvailablePluginListCommandResponse> QueryAvailableList([FromBody] QueryTeamAvailablePluginListCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Collaborator)
        {
            throw new BusinessException("没有操作权限");
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队插件详情.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("detail")]
    public async Task<QueryTeamPluginDetailCommandResponse> QueryDetail([FromBody] QueryTeamPluginDetailCommand req, CancellationToken ct = default)
    {
        await _mediator.Send(new CheckPluginBelongsToTeamCommand { PluginId = req.PluginId, TeamId = req.TeamId });
        await CheckUserIsTeamAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 插件的函数列表.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryCustomPluginFunctionsListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryCustomPluginFunctionsListCommandResponse"/>.</returns>
    [HttpPost("function_list")]
    public async Task<QueryCustomPluginFunctionsListCommandResponse> QueryPluginFunctionsList([FromBody] QueryTeamCustomPluginFunctionsListCommand req, CancellationToken ct = default)
    {
        await _mediator.Send(new CheckPluginBelongsToTeamCommand { PluginId = req.PluginId, TeamId = req.TeamId });

        await CheckUserIsTeamAdminAsync(req.TeamId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 刷新 mcp 服务器的 tool 列表.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="RefreshMcpServerPluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("refresh_mcp")]
    public async Task<EmptyCommandResponse> RefreshMcp([FromBody] RefreshTeamMcpServerPluginCommand req, CancellationToken ct = default)
    {
        await _mediator.Send(new CheckPluginBelongsToTeamCommand { PluginId = req.PluginId, TeamId = req.TeamId });

        await CheckUserIsTeamAdminAsync(req.TeamId, ct);

        return await _mediator.Send(
            new RefreshMcpServerPluginCommand
            {
                PluginId = req.PluginId
            },
            ct);
    }

    /// <summary>
    /// 修改 mcp 插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="UpdateMcpServerPluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_mcp")]
    public async Task<EmptyCommandResponse> UpdateMcp([FromBody] UpdateTeamMcpServerPluginCommand req, CancellationToken ct = default)
    {
        await _mediator.Send(new CheckPluginBelongsToTeamCommand { PluginId = req.PluginId, TeamId = req.TeamId });

        await CheckUserIsTeamAdminAsync(req.TeamId, ct);

        return await _mediator.Send(
            new UpdateMcpServerPluginCommand
            {
                PluginId = req.PluginId,
                Name = req.Name,
                Description = req.Description,
                ClassifyId = req.ClassifyId,
                IsPublic = false,
                Header = req.Header,
                Query = req.Query,
                ServerUrl = req.ServerUrl,
                Title = req.Title,
            },
            ct);
    }

    /// <summary>
    /// 修改 openapi 插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="UpdateOpenApiPluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_openapi")]
    public async Task<EmptyCommandResponse> UpdateOpenApi([FromBody] UpdateTeamOpenApiPluginCommand req, CancellationToken ct = default)
    {
        await _mediator.Send(new CheckPluginBelongsToTeamCommand { PluginId = req.PluginId, TeamId = req.TeamId });

        await CheckUserIsTeamAdminAsync(req.TeamId, ct);

        return await _mediator.Send(
            new UpdateOpenApiPluginCommand
            {
                PluginId = req.PluginId,
                FileId = req.FileId,
                FileName = req.FileName,
                Name = req.Name,
                Title = req.Title,
                Description = req.Description,
                ClassifyId = req.ClassifyId,
                IsPublic = false,
                ServerUrl = req.ServerUrl,
                Header = req.Header,
                Query = req.Query,
            },
            ct);
    }

    private async Task CheckUserIsTeamAdminAsync(int teamId, CancellationToken ct)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = teamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Admin)
        {
            throw new BusinessException("没有操作权限");
        }
    }
}
