using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.DingTalk.Commands;

/// <summary>
/// 创建钉钉应用连接，需要团队 Admin 及以上角色；AppKey 全局唯一，创建后立即建立 Stream 长连接；
/// 可携带渠道信息创建即绑定（当前仅应用渠道）.
/// </summary>
public class CreateDingTalkAppCommand : IRequest<SimpleGuid>, IUserIdContext, IModelValidator<CreateDingTalkAppCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 连接名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 钉钉应用 AppKey（新版开发者后台为 Client ID）.
    /// </summary>
    public string AppKey { get; init; } = default!;

    /// <summary>
    /// 钉钉应用 AppSecret（新版开发者后台为 Client Secret）.
    /// </summary>
    public string AppSecret { get; init; } = default!;

    /// <summary>
    /// 创建即绑定的渠道类型，可为空（不绑定）；当前仅支持应用渠道 app.
    /// </summary>
    public DingTalkChannelType? ChannelType { get; init; }

    /// <summary>
    /// 创建即绑定的渠道记录 id 字符串，应用渠道为 app.id（uuid）；与 <see cref="ChannelType"/> 必须同时提供或同时缺省.
    /// </summary>
    public string? ChannelId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateDingTalkAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("连接名称不能为空.").MaximumLength(50).WithMessage("连接名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.AppKey).NotEmpty().WithMessage("钉钉 AppKey 不能为空.").MaximumLength(64).WithMessage("钉钉 AppKey 最长 64 个字符.");
        validate.RuleFor(x => x.AppSecret).NotEmpty().WithMessage("钉钉 AppSecret 不能为空.").MaximumLength(128).WithMessage("钉钉 AppSecret 最长 128 个字符.");
        validate.RuleFor(x => x.ChannelType).IsInEnum().WithMessage("渠道类型不正确.").When(x => x.ChannelType.HasValue);
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.").MaximumLength(64).WithMessage("渠道 id 最长 64 个字符.").When(x => x.ChannelType.HasValue);
        validate.RuleFor(x => x.ChannelType).NotNull().WithMessage("渠道类型与渠道 id 必须同时提供.").When(x => !string.IsNullOrWhiteSpace(x.ChannelId));
    }
}
