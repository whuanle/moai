using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 创建应用接入（团队下的 key，应用 token 可访问其所属团队的资源），需要团队 Admin 及以上角色.
/// </summary>
public class CreateAccessAppCommand : IRequest<CreateAccessAppCommandResponse>, IUserIdContext, IModelValidator<CreateAccessAppCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 功能范围代码列表（model/wiki_read/wiki_write/wiki_mcp），model 授权应用接入 key 直连使用模型网关，
    /// 知识库维度限制该接入签发 token 的知识库范围；不传默认读写全量，空列表=纯对话接入（token 无知识库权限）.
    /// </summary>
    public List<string>? Scopes { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAccessAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(20).WithMessage("接入名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("接入描述最长 255 个字符.");
        validate.RuleFor(x => x.Scopes)
            .Must(x => x == null || TeamApiKeyScopeCodes.TryParseAccessAppCodes(x.Distinct(), out _))
            .WithMessage("功能范围代码不合法.")
            .Must(x => x == null || x.Count == x.Distinct().Count())
            .WithMessage("功能范围代码不能重复.");
    }
}
