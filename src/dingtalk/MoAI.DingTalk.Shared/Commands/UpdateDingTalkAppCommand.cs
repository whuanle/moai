using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.DingTalk.Commands;

/// <summary>
/// 更新钉钉应用连接，需要团队 Admin 及以上角色；AppSecret 为空表示保持不变，更新后自动重连.
/// </summary>
public class UpdateDingTalkAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateDingTalkAppCommand>
{
    /// <summary>
    /// 钉钉应用记录 id（来自路由）.
    /// </summary>
    public Guid DingTalkAppId { get; init; }

    /// <summary>
    /// 钉钉应用 AppKey（Client ID），为空表示保持不变；修改后自动重连.
    /// </summary>
    public string? AppKey { get; init; }

    /// <summary>
    /// 连接名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 钉钉应用 AppSecret，为空表示保持不变.
    /// </summary>
    public string? AppSecret { get; init; }

    /// <summary>
    /// 是否禁用，禁用后断开长连接且不再接收事件.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateDingTalkAppCommand> validate)
    {
        validate.RuleFor(x => x.AppKey).MaximumLength(64).WithMessage("钉钉 AppKey 最长 64 个字符.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("连接名称不能为空.").MaximumLength(50).WithMessage("连接名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.AppSecret).MaximumLength(128).WithMessage("钉钉 AppSecret 最长 128 个字符.");
    }
}
