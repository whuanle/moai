using MoAI.Infra.Models;
using MoAI.Settings.Commands;
using MoAI.Settings.Queries.Responses;

namespace MoAI.Settings.Services;

/// <summary>
/// 设置领域服务，封装设置项的校验、查询与更新逻辑.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 获取全部内置设置项及当前值.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySettingsCommandResponse"/>，包含全部设置项.</returns>
    Task<QuerySettingsCommandResponse> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 保存指定的设置项 key 对应的值（key 校验不通过时抛异常，数据库不存在时自动创建）.
    /// </summary>
    /// <param name="command">保存命令.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    Task<EmptyCommandResponse> SaveSettingAsync(SaveSettingCommand command, CancellationToken cancellationToken);
}
