using System;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamAgent.Commands;

/// <summary>
/// 设置团队智能体头像；objectKey 需为已完成上传并登记的文件，需要团队 Admin 及以上角色.
/// </summary>
public class UpdateTeamAgentAvatarCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamAgentAvatarCommand>, IUserIdContext
{
    /// <summary>
    /// 智能体 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AgentId { get; init; }

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
    public static void Validate(AbstractValidator<UpdateTeamAgentAvatarCommand> validate)
    {
        // AgentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.ObjectKey).NotEmpty().WithMessage("头像文件不能为空.").MaximumLength(255).WithMessage("头像 objectKey 不正确.");
    }
}
