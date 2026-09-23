using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Skill.Commands;

/// <summary>
/// 设置技能头像；objectKey 需为已完成上传并登记的文件；个人技能归属人、团队技能团队管理员或平台管理员.
/// </summary>
public class UpdateSkillAvatarCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateSkillAvatarCommand>
{
    /// <summary>
    /// 技能 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid SkillId { get; init; }

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
    public static void Validate(AbstractValidator<UpdateSkillAvatarCommand> validate)
    {
        // SkillId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.ObjectKey).NotEmpty().WithMessage("头像文件不能为空.");
    }
}
