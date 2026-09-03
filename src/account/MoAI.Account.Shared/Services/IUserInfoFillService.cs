using MoAI.Infra.Models;

namespace MoAI.Account.Services;

/// <summary>
/// 用户信息填充领域服务，为实现/继承 <see cref="AuditsInfo"/> 的模型批量查询并填充创建人、更新人名称.
/// </summary>
public interface IUserInfoFillService
{
    /// <summary>
    /// 填充单个模型的审计用户名称.
    /// </summary>
    /// <param name="item">需要填充的模型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task FillAsync(AuditsInfo item, CancellationToken cancellationToken);

    /// <summary>
    /// 填充模型集合的审计用户名称.
    /// </summary>
    /// <param name="items">需要填充的模型集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task FillAsync(IEnumerable<AuditsInfo> items, CancellationToken cancellationToken);
}
