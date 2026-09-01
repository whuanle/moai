using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 更新用户头像.
/// </summary>
public class UpdateUserAvatarCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 头像文件在存储中的 ObjectKey.
    /// </summary>
    public string ObjectKey { get; set; } = default!;
}
