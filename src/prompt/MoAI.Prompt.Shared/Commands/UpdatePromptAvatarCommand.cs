using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 设置提示词头像；个人提示词仅创建人可设置，团队提示词需要团队 Admin 及以上角色；objectKey 需为已完成上传并登记的文件.
/// </summary>
public class UpdatePromptAvatarCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdatePromptAvatarCommand>
{
    /// <summary>
    /// 提示词 id，由 Controller 从路由参数回填.
    /// </summary>
    public int PromptId { get; init; }

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
    public static void Validate(AbstractValidator<UpdatePromptAvatarCommand> validate)
    {
        // PromptId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.ObjectKey).NotEmpty().WithMessage("头像文件不能为空.");
    }
}
