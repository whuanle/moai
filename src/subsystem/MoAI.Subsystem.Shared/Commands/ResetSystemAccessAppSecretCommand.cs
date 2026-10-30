using MediatR;
using MoAI.Subsystem.Commands.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 重置系统应用接入密钥（root）：生成新密钥并 PBKDF2 落库，原文仅在本次结果中返回一次；旧密钥立即失效.
/// </summary>
public class ResetSystemAccessAppSecretCommand : IRequest<ResetSystemAccessAppSecretCommandResponse>
{
    /// <summary>
    /// 接入 id（由路由填充）.
    /// </summary>
    public Guid Id { get; set; }
}
