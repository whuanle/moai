using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 回显系统级接入密钥原文（仅 root，飞书式随时查看）：
/// 传输走 RSA+AES 信封——请求体携带 RSA(PKCS1) 加密的 base64(AES-256 key)，服务端以该 key 加密原文返回，端到端密文传输.
/// 存量 PBKDF2 哈希行不可回显（400 提示重置）.
/// </summary>
public class RevealSubsysAppSecretCommand : IRequest<RevealSubsysAppSecretCommandResponse>, IModelValidator<RevealSubsysAppSecretCommand>
{
    /// <summary>
    /// 接入 id（由路由回填）.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 信封对称密钥：前端随机生成 32 字节 AES-256 key，base64 编码后经平台 RSA 公钥（PKCS1）加密.
    /// </summary>
    public string EncryptedKey { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RevealSubsysAppSecretCommand> validate)
    {
        validate.RuleFor(x => x.EncryptedKey).NotEmpty().WithMessage("信封密钥不能为空.").MaximumLength(1024).WithMessage("信封密钥过长.");
    }
}
