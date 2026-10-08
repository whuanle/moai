using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// 获取当前用户信息（静态插件）：读取执行引擎注入的调用方上下文，返回用户身份信息.
/// </summary>
[AiPlugin(
    key: "static_current_user",
    Name = "获取当前用户信息",
    Description = "获取当前调用者的用户身份信息（用户名/昵称/邮箱/团队等），信息由执行上下文注入，无需传参")]
public class CurrentUserPlugin : IStaticPluginRuntime<CurrentUserRequest, CurrentUserResponse>
{
    private readonly IPluginRunContextAccessor _runContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentUserPlugin"/> class.
    /// </summary>
    /// <param name="runContextAccessor">插件执行上下文访问器（由执行引擎在本次执行作用域内注入上下文）.</param>
    public CurrentUserPlugin(IPluginRunContextAccessor runContextAccessor)
    {
        _runContextAccessor = runContextAccessor;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        // 此插件无需参数，用户信息来自执行上下文注入
        return "{}";
    }

    /// <inheritdoc/>
    public async Task<CurrentUserResponse> RunAsync(CurrentUserRequest request, CancellationToken cancellationToken)
    {
        var context = _runContextAccessor.Context;
        if (context == null)
        {
            return new CurrentUserResponse { IsContextInjected = false };
        }

        // 用户详细信息仅对已认证调用方有意义，匿名/外部应用身份不做查询
        var user = context.IsAuthenticated
            ? await _runContextAccessor.GetUserAsync(cancellationToken).ConfigureAwait(false)
            : null;
        return new CurrentUserResponse
        {
            IsContextInjected = true,
            IsAuthenticated = context.IsAuthenticated,
            UserId = context.UserId,
            UserName = user?.UserName ?? string.Empty,
            NickName = user?.NickName ?? string.Empty,
            Email = user?.Email ?? string.Empty,
            IsAdmin = user?.IsAdmin ?? false,
            UserType = context.UserType.ToString().ToLowerInvariant(),
            TeamId = context.TeamId,
            Source = context.Source.ToString().ToLowerInvariant(),
        };
    }
}
