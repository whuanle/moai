using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Aggregates;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 保存应用安全配置（内容脱敏规则），需要团队 Admin 及以上角色；Agent 应用与流程应用通用.
/// </summary>
public class SaveAppSecurityCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<SaveAppSecurityCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 是否启用内容脱敏；启用且存在至少一条规则时生效.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 是否处理工具调用结果（含错误信息、流程节点输出）.
    /// </summary>
    public bool MaskToolResult { get; init; }

    /// <summary>
    /// 是否处理工具调用参数（调用记录展示、流程节点输入）.
    /// </summary>
    public bool MaskToolArgs { get; init; }

    /// <summary>
    /// 是否处理模型回复文本（对话正文）.
    /// </summary>
    public bool MaskModelOutput { get; init; }

    /// <summary>
    /// 是否处理用户输入文本（发送给模型的用户消息）.
    /// </summary>
    public bool MaskInput { get; init; }

    /// <summary>
    /// 内容规则列表（用户输入/工具调用结果/工具调用参数范围共用），最多 50 条；type 取 AppSecurityRuleTypes（phone/idCard/email/bankCard/custom），custom 须携带可编译正则；strategy 取 AppSecurityStrategies（redact/mask/hash/block/flag）.
    /// </summary>
    public IReadOnlyList<AppSecurityPolicy.AppSecurityRule> Rules { get; init; } = [];

    /// <summary>
    /// 模型回复专属规则列表，与 <see cref="Rules"/> 相互独立维护；条数与校验规则同 <see cref="Rules"/>.
    /// block 策略走流式缓冲窗拦截（积压阈值释放、命中截断输出），非流式整条替换.
    /// </summary>
    public IReadOnlyList<AppSecurityPolicy.AppSecurityRule> ModelOutputRules { get; init; } = [];

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveAppSecurityCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        ApplyRuleSetRules(validate.RuleFor(x => x.Rules));
        ApplyRuleSetRules(validate.RuleFor(x => x.ModelOutputRules));
    }

    private static void ApplyRuleSetRules(IRuleBuilderInitial<SaveAppSecurityCommand, IReadOnlyList<AppSecurityPolicy.AppSecurityRule>> rule)
    {
        rule.Must(rules => rules.Count <= AppSecurityPolicy.MaxRuleCount)
            .WithMessage($"脱敏规则最多 {AppSecurityPolicy.MaxRuleCount} 条.");
        rule.Must(rules => rules.All(r => string.IsNullOrWhiteSpace(r.Name) || r.Name.Length <= 50))
            .WithMessage("规则名称最长 50 个字符.");
        rule.Must(rules => rules.All(r => string.IsNullOrWhiteSpace(r.Replacement) || r.Replacement.Length <= 50))
            .WithMessage("规则替换文本最长 50 个字符.");
        rule.Must(rules => rules.All(r => AppSecurityRuleTypes.IsValid(r.Type)))
            .WithMessage("规则类型仅支持 phone/idCard/email/bankCard/injection/custom.");
        rule.Must(rules => rules.All(r => string.IsNullOrEmpty(r.Strategy) || AppSecurityStrategies.IsValid(r.Strategy)))
            .WithMessage("处理策略仅支持 redact/mask/hash/block/flag.");
        rule.Must(rules => rules.All(r => r.KeepLast is null || (r.KeepLast >= 0 && r.KeepLast <= 18)))
            .WithMessage("mask 保留末位需在 0~18 之间.");
        rule.Must(rules => rules.All(r =>
                r.Type != AppSecurityRuleTypes.Custom || (!string.IsNullOrWhiteSpace(r.Pattern) && r.Pattern.Length <= 500)))
            .WithMessage("自定义规则必须携带不超过 500 字符的正则表达式.");
        rule.Must(rules => rules.All(r =>
                r.Type != AppSecurityRuleTypes.Custom || AppSecurityPolicy.CompileRule(r.Type, r.Pattern) != null))
            .WithMessage("自定义规则的正则表达式不合法.");
    }
}
