using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.CustomPlugins.Commands.Responses;
using MoAI.Plugin.CustomPlugins.Queries;
using MoAI.Plugin.CustomPlugins.Queries.Responses;

namespace MoAI.Plugin.Controllers;

/// <summary>
/// 自定义插件.
/// </summary>
[ApiController]
[Route("/admin/custom_plugin")]
public partial class CustomPluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPluginController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR mediator instance.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public CustomPluginController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 删除插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="DeletePluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("delete_plugin")]
    public async Task<EmptyCommandResponse> DeletePlugin([FromBody] DeletePluginCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(new DeletePluginCommand { PluginId = req.PluginId }, ct);
    }

    /// <summary>
    /// 导入 mcp 服务.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="ImportMcpServerPluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleInt"/>.</returns>
    [HttpPost("import_mcp")]
    public async Task<SimpleInt> ImportMcp([FromBody] ImportMcpServerPluginCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 完成 openapi 文件上传.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="ImportOpenApiPluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleInt"/>.</returns>
    [HttpPost("import_openapi")]
    public async Task<SimpleInt> ImportOpenApi([FromBody] ImportOpenApiPluginCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预上传 openapi 文件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="PreUploadOpenApiFilePluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadOpenApiFilePluginCommandResponse"/>.</returns>
    [HttpPost("pre_upload_openapi")]
    public async Task<PreUploadOpenApiFilePluginCommandResponse> PreUploadOpenApi([FromBody] PreUploadOpenApiFilePluginCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询插件简要信息列表，不支持查询内置插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryPluginBaseListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginBaseListCommandResponse"/>.</returns>
    [HttpPost("plugin_list")]
    public async Task<QueryPluginBaseListCommandResponse> QueryPluginBaseList([FromBody] QueryPluginBaseListCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取插件的详细信息.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryPluginDetailCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginDetailCommandResponse"/>.</returns>
    [HttpPost("plugin_detail")]
    public async Task<QueryPluginDetailCommandResponse> QueryPluginDetail([FromBody] QueryPluginDetailCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 插件的函数列表.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryPluginFunctionsListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginFunctionsListCommandResponse"/>.</returns>
    [HttpPost("function_list")]
    public async Task<QueryPluginFunctionsListCommandResponse> QueryPluginFunctionsList([FromBody] QueryPluginFunctionsListCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 刷新 mcp 服务器的 tool 列表.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="RefreshMcpServerPluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("refresh_mcp")]
    public async Task<EmptyCommandResponse> RefreshMcp([FromBody] RefreshMcpServerPluginCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

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
    public async Task<EmptyCommandResponse> UpdateMcp([FromBody] UpdateMcpServerPluginCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改 openapi 插件.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="UpdateOpenApiPluginCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_openapi")]
    public async Task<EmptyCommandResponse> UpdateOpenApi([FromBody] UpdateOpenApiPluginCommand req, CancellationToken ct = default)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}