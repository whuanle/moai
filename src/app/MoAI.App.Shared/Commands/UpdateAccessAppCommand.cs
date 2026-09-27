using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 更新应用接入（名称、描述、功能范围；key 不可改），需要团队 Admin 及以上角色.
/// </summary>
public class UpdateAccessAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateAccessAppCommand>
{
    /// <summary>
    /// 接入 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AccessAppId { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 新功能范围代码列表（model/wiki_read/wiki_write/wiki_mcp），null=不修改；空列表=纯对话接入.
    /// </summary>
    public List<string>? Scopes { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAccessAppCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(20).WithMessage("接入名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("接入描述最长 255 个字符.");
        validate.RuleFor(x => x.Scopes)
            .Must(x => x == null || TeamApiKeyScopeCodes.TryParseAccessAppCodes(x.Distinct(), out _))
            .WithMessage("功能范围代码不合法.")
            .Must(x => x == null || x.Count == x.Distinct().Count())
            .WithMessage("功能范围代码不能重复.");
    }
}
