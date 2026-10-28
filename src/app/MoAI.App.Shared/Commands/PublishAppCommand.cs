using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 发布应用，发布后团队成员可进入应用进行对话；需要团队 Admin 及以上角色，仅 Agent 应用可发布.
/// </summary>
public class PublishAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<PublishAppCommand>
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 应用版本号 aaa.bb.cc（如 1.0.3），可空；携带时必须严格大于应用当前版本（更新当前版本并随发布快照生效），不携带时保持原版本号.
    /// 存库编码 aaa*10000 + bb*100 + cc.
    /// </summary>
    public string? Version { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PublishAppCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Version).Matches(AppVersionHelper.Pattern).When(x => !string.IsNullOrWhiteSpace(x.Version))
            .WithMessage("应用版本号格式不正确，应为 aaa.bb.cc（如 1.0.3）.");
    }
}
