using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 删除应用（软删除并保留历史），需要团队 Admin 及以上角色.
/// </summary>
public class DeleteAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<DeleteAppCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAppCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不能为空.");
    }
}
