using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 设置应用头像，需要团队 Admin 及以上角色；objectKey 需为已完成上传并登记的文件.
/// </summary>
public class UpdateAppAvatarCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateAppAvatarCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 头像文件的 ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAppAvatarCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.ObjectKey).NotEmpty().WithMessage("头像文件不能为空.");
    }
}
