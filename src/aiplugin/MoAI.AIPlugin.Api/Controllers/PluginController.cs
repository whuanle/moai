using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;

namespace MoAI.AIPlugin.Controllers;

/// <summary>
/// 插件引擎管理接口（仅管理员）.
/// </summary>
[ApiController]
[Route("/ai/plugin")]
public class PluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public PluginController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询已发现插件列表（仅管理员）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginListCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryPluginListCommandResponse> QueryAll(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryPluginListCommand(), ct);
    }

    /// <summary>
    /// 查询插件的工具清单（仅管理员）。多工具动态插件返回全部工具，单工具插件固定一个 key=run 的项.
    /// </summary>
    /// <param name="key">插件 key（模板 key 或动态插件实例 key）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginToolsCommandResponse"/>.</returns>
    [HttpGet("tools")]
    public async Task<QueryPluginToolsCommandResponse> QueryTools([FromQuery] string key, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryPluginToolsCommand { Key = key }, ct);
    }

    /// <summary>
    /// 查询单个工具的参数运行示例 JSON（仅管理员）.
    /// </summary>
    /// <param name="key">插件 key（模板 key 或动态插件实例 key）.</param>
    /// <param name="toolKey">工具 key（单工具插件传 run）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginToolExampleCommandResponse"/>.</returns>
    [HttpGet("tool/example")]
    public async Task<QueryPluginToolExampleCommandResponse> QueryToolExample([FromQuery] string key, [FromQuery] string toolKey, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryPluginToolExampleCommand { Key = key, ToolKey = toolKey }, ct);
    }

    /// <summary>
    /// 执行插件（仅管理员）.
    /// </summary>
    /// <param name="req">执行请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PluginRunResult"/>.</returns>
    [HttpPost("run")]
    public async Task<PluginRunResult> Run([FromBody] RunPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>查询 MoPlatform 在线插件目录（仅管理员）.</summary>
    [HttpGet("online/catalog")]
    public async Task<PluginMarketplacePage> QueryOnlineCatalog([FromQuery] QueryPluginMarketplaceCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>查询 MoPlatform 在线插件分类（仅管理员）.</summary>
    [HttpGet("online/categories")]
    public async Task<IReadOnlyList<PluginMarketplaceCategory>> QueryOnlineCategories(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryPluginMarketplaceCategoriesCommand(), ct);
    }

    /// <summary>查询 MoAI 已安装在线插件（仅管理员）.</summary>
    [HttpGet("online/installed")]
    public async Task<IReadOnlyList<InstalledPluginItem>> QueryInstalledOnlinePlugins(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryInstalledPluginsCommand(), ct);
    }

    /// <summary>从 MoPlatform 下载并安装插件包（仅管理员）.</summary>
    [HttpPost("online/install")]
    public async Task<InstalledPluginItem> InstallOnlinePlugin([FromBody] InstallOnlinePluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>上架或下架本地在线插件；下架不删除包、不卸载程序集（仅管理员）.</summary>
    [HttpPost("online/status")]
    public async Task<InstalledPluginItem> SetOnlinePluginStatus([FromBody] SetOnlinePluginStatusCommand req, CancellationToken ct)
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
