using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 设置会话绑定的专家提示词；仅会话归属用户可操作，promptId=0 表示清除绑定.
/// </summary>
public class UpdateAppSessionPromptCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateAppSessionPromptCommand>
{
    /// <summary>
    /// 会话 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 专家提示词 id，0 表示清除绑定.
    /// </summary>
    public int PromptId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAppSessionPromptCommand> validate)
    {
        // SessionId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.PromptId).GreaterThanOrEqualTo(0).WithMessage("提示词 id 不正确.");
    }
}
