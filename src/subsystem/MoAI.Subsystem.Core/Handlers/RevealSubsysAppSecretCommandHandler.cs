using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Commands.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="RevealSubsysAppSecretCommand"/>
/// </summary>
public class RevealSubsysAppSecretCommandHandler : IRequestHandler<RevealSubsysAppSecretCommand, RevealSubsysAppSecretCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemSecretCipher _secretCipher;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevealSubsysAppSecretCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="secretCipher">密钥可逆加密服务.</param>
    /// <param name="rsaProvider">RSA 服务（解信封 key）.</param>
    public RevealSubsysAppSecretCommandHandler(DatabaseContext databaseContext, ISubsystemSecretCipher secretCipher, IRsaProvider rsaProvider)
    {
        _databaseContext = databaseContext;
        _secretCipher = secretCipher;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<RevealSubsysAppSecretCommandResponse> Handle(RevealSubsysAppSecretCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new BusinessException("接入不存在.") { StatusCode = 404 };

        // 存量 PBKDF2 哈希不可逆：无法回显，提示重置（重置后转为 v2 可回显格式）
        if (!_secretCipher.TryReveal(app.Secret ?? string.Empty, out var secret))
        {
            throw new BusinessException("该接入的密钥为存量哈希存储，无法查看，请重置密钥后查看.") { StatusCode = 400 };
        }

        byte[] envelopeKey;
        try
        {
            envelopeKey = Convert.FromBase64String(_rsaProvider.Decrypt(request.EncryptedKey));
        }
        catch (FormatException)
        {
            throw new BusinessException("信封密钥格式无效.") { StatusCode = 400 };
        }

        if (envelopeKey.Length != SubsystemSecretEnvelope.KeySizeBytes)
        {
            throw new BusinessException("信封密钥长度无效.") { StatusCode = 400 };
        }

        var (iv, data) = SubsystemSecretEnvelope.Encrypt(envelopeKey, secret);
        return new RevealSubsysAppSecretCommandResponse
        {
            Mask = _secretCipher.Mask(secret),
            Iv = iv,
            Data = data,
        };
    }
}
