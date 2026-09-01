using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Settings.Commands;

/// <summary>
/// 保存设置.
/// </summary>
public class SaveSettingCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 设置项 key.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 设置项值.
    /// </summary>
    public string Value { get; init; } = default!;
}
