using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 重命名会话；仅会话归属用户可操作.
/// </summary>
public class UpdateAppSessionTitleCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateAppSessionTitleCommand>
{
    /// <summary>
    /// 会话 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 新标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAppSessionTitleCommand> validate)
    {
        // SessionId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Title).NotEmpty().WithMessage("会话标题不能为空.").MaximumLength(100).WithMessage("会话标题最长 100 个字符.");
    }
}
