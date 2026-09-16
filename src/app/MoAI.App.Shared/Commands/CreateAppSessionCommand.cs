using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 创建 Agent 应用会话；团队成员可创建，Member 仅能对已发布应用创建会话.
/// </summary>
public class CreateAppSessionCommand : IRequest<SimpleGuid>, IUserIdContext, IModelValidator<CreateAppSessionCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 会话标题，可为空；空则由后端置为「未命名标题」.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 绑定的专家提示词 id，0 表示不绑定；创建后可通过更新会话提示词接口调整.
    /// </summary>
    public int PromptId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAppSessionCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Title).MaximumLength(100).WithMessage("会话标题最长 100 个字符.");
    }
}
