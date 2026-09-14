using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 外部应用换取 token：第三方应用使用应用接入 key 换取应用 token 或用户 token，也支持 is_auth=false 应用的匿名换 token.
/// 三种调用形态：
/// 1. 应用 token：只提供 AccessAppKey，授权范围为该接入配置的全部应用；
/// 2. 用户 token：提供 AccessAppKey + AppId + ExternalUserId，绑定外部用户且仅授权单个应用；
/// 3. 匿名 token：只提供 AppId（应用须 is_external=true 且 is_auth=false），生成/复用临时外部身份.
/// </summary>
public class ExternalTokenCommand : IRequest<ExternalTokenCommandResponse>, IModelValidator<ExternalTokenCommand>
{
    /// <summary>
    /// 应用接入 key（moai-ac- 前缀），应用 token 与用户 token 必填.
    /// </summary>
    public string? AccessAppKey { get; init; }

    /// <summary>
    /// 目标应用 id，用户 token 与匿名 token 必填.
    /// </summary>
    public Guid? AppId { get; init; }

    /// <summary>
    /// 外部用户标识（第三方系统的用户唯一 id），用户 token 必填；匿名 token 可选，提供时复用同一外部身份.
    /// </summary>
    public string? ExternalUserId { get; init; }

    /// <summary>
    /// 外部用户显示名，可选.
    /// </summary>
    public string? Nickname { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExternalTokenCommand> validate)
    {
        validate.RuleFor(x => x.AccessAppKey)
            .Must(x => x == null || x.StartsWith("moai-ac-", StringComparison.Ordinal))
            .WithMessage("接入 key 格式不正确.")
            .MaximumLength(255)
            .WithMessage("接入 key 过长.");
        validate.RuleFor(x => x.ExternalUserId)
            .MaximumLength(128)
            .WithMessage("外部用户标识最长 128 个字符.");
        validate.RuleFor(x => x.Nickname)
            .MaximumLength(100)
            .WithMessage("外部用户显示名最长 100 个字符.");
        validate.RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.AccessAppKey) || x.AppId != null)
            .WithMessage("accessAppKey 与 appId 至少提供一个.");
    }
}
