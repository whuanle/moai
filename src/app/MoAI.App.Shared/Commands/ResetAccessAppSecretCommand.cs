using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 重置应用接入密钥（团队级）：生成新密钥并 PBKDF2 落库，原文仅在本次结果中返回一次；旧密钥立即失效.
/// 需要团队 Admin 及以上角色.
/// </summary>
public class ResetAccessAppSecretCommand : IRequest<ResetAccessAppSecretCommandResponse>, IUserIdContext, IModelValidator<ResetAccessAppSecretCommand>
{
    /// <summary>
    /// 接入 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AccessAppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetAccessAppSecretCommand> validate)
    {
        validate.RuleFor(x => x.AccessAppId).NotEqual(Guid.Empty).WithMessage("接入 id 不正确.");
    }
}
