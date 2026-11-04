using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 收藏应用：仅限应用广场可见的应用（已发布、已公开、未禁用、非外部且在可视范围内），重复收藏幂等返回成功.
/// </summary>
public class FavoriteAppCommand : IRequest<EmptyCommandResponse>, IModelValidator<FavoriteAppCommand>, IUserIdContext
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
    public static void Validate(AbstractValidator<FavoriteAppCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}
