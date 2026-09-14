using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 创建外部会话：外部用户 token 对其授权范围内已发布的 Agent 外部应用发起会话.
/// 会话归属 external_user.id（user_type=External），后续对话以此校验归属.
/// </summary>
public class CreateExternalAgentSessionCommand : IRequest<SimpleGuid>, IModelValidator<CreateExternalAgentSessionCommand>
{
    /// <summary>
    /// 目标应用 id（路由参数）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 会话标题，可选（默认「未命名标题」）.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 外部 token 上下文，由 ExternalController 从外部 token claims 填充.
    /// </summary>
    [JsonIgnore]
    public ExternalTokenContext Context { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateExternalAgentSessionCommand> validate)
    {
        // AppId 来自路由模板的 guid 约束（非请求体），不做 NotEmpty 校验
        validate.RuleFor(x => x.Title).MaximumLength(100).WithMessage("会话标题最长 100 个字符.");
    }
}
