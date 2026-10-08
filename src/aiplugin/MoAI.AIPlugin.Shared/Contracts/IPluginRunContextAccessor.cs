using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Contracts;

/// <summary>
/// 插件执行上下文访问器（每次插件执行一个实例）.
/// 执行引擎在隔离作用域内调用 <see cref="Set"/> 注入调用方上下文；
/// 插件构造注入本接口读取上下文，或经 <see cref="GetUserAsync"/> 懒加载当前用户详细信息.
/// </summary>
public interface IPluginRunContextAccessor
{
    /// <summary>
    /// 当前执行上下文；执行入口未注入（历史调用路径）时为 null.
    /// </summary>
    PluginRunContext? Context { get; }

    /// <summary>
    /// 注入执行上下文，仅供执行引擎在实例化插件前调用.
    /// </summary>
    /// <param name="context">执行上下文.</param>
    void Set(PluginRunContext? context);

    /// <summary>
    /// 查询当前用户详细信息（用户名/昵称/邮箱等，来自用户表）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>上下文缺失、未认证或用户已删除时返回 null.</returns>
    Task<PluginRunUser?> GetUserAsync(CancellationToken cancellationToken);
}
