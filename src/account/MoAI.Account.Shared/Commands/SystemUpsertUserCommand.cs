using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Account.Queries.Responses;
using MoAI.Infra.Services;

namespace MoAI.Account.Commands;

/// <summary>
/// 系统级用户写入（/api/subsys/system/*，系统应用接入 user_write 范围）：
/// 按 userName 幂等 upsert（已存在则更新非空字段），适配 ERP 等外部系统定时推送用户.
/// </summary>
public class SystemUpsertUserCommand : IRequest<SystemUpsertUserCommandResponse>, IModelValidator<SystemUpsertUserCommand>
{
    /// <summary>
    /// 用户名（唯一业务键，幂等推送的匹配键）.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 昵称，可选；创建时缺省取用户名.
    /// </summary>
    public string? NickName { get; init; }

    /// <summary>
    /// 手机号，可选；null=不修改.
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>
    /// 邮箱，可选；null=不修改.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// 初始密码，可选（明文，走 TLS 传输）；缺省时创建路径自动生成随机密码并在响应中一次性返回；更新路径 null=不修改.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// 是否禁用，可选；null=不修改（创建时缺省为启用）.
    /// </summary>
    public bool? IsDisable { get; init; }

    /// <summary>
    /// 第三方登录渠道 id（oauth_connection.id），可选；与 <see cref="OauthSub"/> 成对出现时，
    /// upsert 用户后幂等绑定第三方身份（后续经 /api/subsys/system/app-tokens 按 oauth 身份换发访问用户 token）.
    /// </summary>
    public Guid? OauthProviderId { get; init; }

    /// <summary>
    /// 第三方用户唯一标识（open_id / unionId / userid），可选；与 <see cref="OauthProviderId"/> 成对出现.
    /// </summary>
    public string? OauthSub { get; init; }

    /// <summary>
    /// 第三方 union_id，可选（飞书场景；与 Sub 一并落库便于按 unionId 匹配）.
    /// </summary>
    public string? OauthUnionId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemUpsertUserCommand> validate)
    {
        validate.RuleFor(x => x.UserName).NotEmpty().WithMessage("用户名不能为空.")
            .Matches("^[a-zA-Z0-9_-]{2,255}$").WithMessage("用户名 2-255 位，仅限字母/数字/下划线/中划线.");
        validate.RuleFor(x => x.NickName).MaximumLength(50).WithMessage("昵称最长 50 字符.");
        validate.RuleFor(x => x.Phone).MaximumLength(20).WithMessage("手机号最长 20 位.")
            .Matches("^1[3-9]\\d{9}$").WithMessage("手机号格式不正确.").When(x => !string.IsNullOrEmpty(x.Phone));
        validate.RuleFor(x => x.Email).EmailAddress().WithMessage("邮箱格式不正确.").MaximumLength(255).WithMessage("邮箱最长 255 字符.")
            .When(x => !string.IsNullOrEmpty(x.Email));
        validate.RuleFor(x => x.Password).MaximumLength(128).WithMessage("密码过长.");
        validate.RuleFor(x => x.OauthSub).NotEmpty().WithMessage("携带 oauthProviderId 时必须提供 oauthSub.")
            .MaximumLength(128).WithMessage("第三方用户标识最长 128 个字符.")
            .When(x => x.OauthProviderId != null);
        validate.RuleFor(x => x.OauthUnionId).MaximumLength(128).WithMessage("第三方 union_id 最长 128 个字符.");
    }
}
