using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 系统级颁发内部应用访问点 token（/api/subsys/system/app-tokens，身份穿透）：
/// 按第三方登录绑定（providerId + sub/unionId）换算站内用户 id，为该用户颁发绑定单个内部应用的访问用户 token.
/// 第三方身份未绑定用户时自动初始化 MoAI 用户并完成 oauth 绑定；已存在绑定则幂等复用.
/// </summary>
public class SystemIssueAppAccessTokenCommand : IRequest<SystemIssueAppAccessTokenCommandResponse>, IModelValidator<SystemIssueAppAccessTokenCommand>
{
    /// <summary>
    /// 目标内部应用 id（须为已发布、访问点已开启的内部应用）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 第三方登录渠道 id（oauth_connection.id）.
    /// </summary>
    public Guid OauthProviderId { get; init; }

    /// <summary>
    /// 第三方用户唯一标识（open_id / unionId / userid，与绑定记录 Sub 同口径）.
    /// </summary>
    public string OauthSub { get; init; } = default!;

    /// <summary>
    /// 第三方用户 union_id，可选；提供时与 sub 一并参与匹配（任一命中即视为同一用户，多命中拒绝）.
    /// </summary>
    public string? OauthUnionId { get; init; }

    /// <summary>
    /// 首次自动建户时使用的昵称，可选；已存在用户不会被本接口修改昵称.
    /// </summary>
    public string? NickName { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemIssueAppAccessTokenCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不能为空.");
        validate.RuleFor(x => x.OauthProviderId).NotEmpty().WithMessage("第三方登录渠道 id 不能为空.");
        validate.RuleFor(x => x.OauthSub).NotEmpty().WithMessage("第三方用户标识不能为空.")
            .MaximumLength(128).WithMessage("第三方用户标识最长 128 个字符.");
        validate.RuleFor(x => x.OauthUnionId).MaximumLength(128).WithMessage("第三方 union_id 最长 128 个字符.");
        validate.RuleFor(x => x.NickName).MaximumLength(50).WithMessage("昵称最长 50 个字符.");
    }
}
