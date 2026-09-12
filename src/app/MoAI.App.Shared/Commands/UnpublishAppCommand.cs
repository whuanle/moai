using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 取消发布应用；需要团队 Admin 及以上角色.
/// </summary>
public class UnpublishAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UnpublishAppCommand>
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UnpublishAppCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不正确.");
    }
}
