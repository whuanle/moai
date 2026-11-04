using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 取消收藏应用：未收藏时幂等返回成功.
/// </summary>
public class UnfavoriteAppCommand : IRequest<EmptyCommandResponse>, IModelValidator<UnfavoriteAppCommand>, IUserIdContext
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
    public static void Validate(AbstractValidator<UnfavoriteAppCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}
