using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Feishu.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Feishu.Queries;

/// <summary>
/// 查询团队下的飞书应用连接列表（含绑定渠道与在线状态），仅团队成员可访问；不回显 AppSecret.
/// </summary>
public class QueryFeishuAppsCommand : IRequest<QueryFeishuAppsCommandResponse>, IUserIdContext, IModelValidator<QueryFeishuAppsCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 关键字，模糊匹配连接名称或飞书 AppID；可为空.
    /// </summary>
    public string? Keyword { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryFeishuAppsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Keyword).MaximumLength(50).WithMessage("关键字最长 50 个字符.");
    }
}
