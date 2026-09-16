using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 保存用户级应用配置：用户对某个应用的个性化定制（专家提示词/自选技能），跨会话复用.
/// </summary>
public class SaveAppUserConfigCommand : IRequest<EmptyCommandResponse>, IModelValidator<SaveAppUserConfigCommand>, IUserIdContext
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 用户选择的专家提示词 id，0=清除（新会话使用应用默认提示词）.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 用户自选技能 id 列表，与应用绑定技能取并集生效；null=不修改.
    /// </summary>
    public IReadOnlyList<Guid>? Skills { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveAppUserConfigCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.PromptId).GreaterThanOrEqualTo(0).WithMessage("提示词 id 不正确.");
        validate.RuleForEach(x => x.Skills).ChildRules(skills =>
        {
            skills.RuleFor(x => x).NotEmpty().WithMessage("技能 id 不正确.");
        });
    }
}
