using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 创建应用调试会话：仅存 Redis 热态、不落库、不计用量，未发布应用也可调试；需要团队 Admin 及以上角色.
/// </summary>
public class CreateDebugSessionCommand : IRequest<SimpleGuid>, IUserIdContext, IModelValidator<CreateDebugSessionCommand>
{
    /// <summary>
    /// 应用 id（来自路由）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateDebugSessionCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不正确.");
    }
}
