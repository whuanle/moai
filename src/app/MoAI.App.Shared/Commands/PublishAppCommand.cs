using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 发布应用，发布后团队成员可进入应用进行对话；需要团队 Admin 及以上角色，仅 Agent 应用可发布.
/// </summary>
public class PublishAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<PublishAppCommand>
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
    public static void Validate(AbstractValidator<PublishAppCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不正确.");
    }
}
