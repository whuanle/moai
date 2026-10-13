using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppSecurityCommand"/>
/// </summary>
public class SaveAppSecurityCommandHandler : IRequestHandler<SaveAppSecurityCommand, EmptyCommandResponse>
{
    private static readonly JsonSerializerOptions RuleJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppSecurityCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SaveAppSecurityCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveAppSecurityCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用安全设置.") { StatusCode = 403 };
        }

        // Controller 手动重建 Command 会绕过 MVC 自动校验，关键校验在 Handler 内兜底重放
        var rules = NormalizeAndValidateRules(request.Rules);
        var modelOutputRules = NormalizeAndValidateRules(request.ModelOutputRules);

        var config = await _databaseContext.AppSecurityConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        if (config == null)
        {
            config = new AppSecurityConfigEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = app.TeamId,
                AppId = app.Id,
            };
            _databaseContext.AppSecurityConfigs.Add(config);
        }

        config.Enabled = request.Enabled;
        config.MaskToolResult = request.MaskToolResult;
        config.MaskToolArgs = request.MaskToolArgs;
        config.MaskModelOutput = request.MaskModelOutput;
        config.MaskInput = request.MaskInput;
        config.Rules = JsonSerializer.Serialize(rules, RuleJsonOptions);
        config.ModelOutputRules = JsonSerializer.Serialize(modelOutputRules, RuleJsonOptions);

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 规则规范化与校验：条数、名称/替换文本长度、类型/策略合法性、custom 正则可编译；不合法抛 400（模型回复 block 走流式缓冲拦截，同样允许）.
    /// </summary>
    internal static List<AppSecurityPolicy.AppSecurityRule> NormalizeAndValidateRules(IReadOnlyList<AppSecurityPolicy.AppSecurityRule>? rules)
    {
        if (rules == null || rules.Count == 0)
        {
            return [];
        }

        if (rules.Count > AppSecurityPolicy.MaxRuleCount)
        {
            throw new BusinessException($"脱敏规则最多 {AppSecurityPolicy.MaxRuleCount} 条.") { StatusCode = 400 };
        }

        var normalized = new List<AppSecurityPolicy.AppSecurityRule>();
        foreach (var rule in rules)
        {
            var name = rule.Name?.Trim() ?? string.Empty;
            var type = rule.Type?.Trim() ?? string.Empty;
            var pattern = rule.Pattern?.Trim() ?? string.Empty;
            // 策略空归一为 redact（兼容存量客户端）
            var strategy = string.IsNullOrWhiteSpace(rule.Strategy) ? AppSecurityStrategies.Redact : rule.Strategy!.Trim();
            // replacement 仅 redact 生效
            var replacement = strategy == AppSecurityStrategies.Redact
                ? (string.IsNullOrEmpty(rule.Replacement?.Trim()) ? AppSecurityPolicy.DefaultReplacement : rule.Replacement!.Trim())
                : string.Empty;

            if (name.Length > 50)
            {
                throw new BusinessException("规则名称最长 50 个字符.") { StatusCode = 400 };
            }

            if (!AppSecurityRuleTypes.IsValid(type))
            {
                throw new BusinessException("规则类型仅支持 phone/idCard/email/bankCard/injection/custom.") { StatusCode = 400 };
            }

            if (!AppSecurityStrategies.IsValid(strategy))
            {
                throw new BusinessException("处理策略仅支持 redact/mask/hash/block/flag.") { StatusCode = 400 };
            }

            if (rule.KeepLast is { } keepLast && (keepLast < 0 || keepLast > 18))
            {
                throw new BusinessException("mask 保留末位需在 0~18 之间.") { StatusCode = 400 };
            }

            if (type == AppSecurityRuleTypes.Custom)
            {
                if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 500)
                {
                    throw new BusinessException("自定义规则必须携带不超过 500 字符的正则表达式.") { StatusCode = 400 };
                }

                if (AppSecurityPolicy.CompileRule(type, pattern) == null)
                {
                    throw new BusinessException("自定义规则的正则表达式不合法.") { StatusCode = 400 };
                }
            }

            normalized.Add(new AppSecurityPolicy.AppSecurityRule
            {
                Name = name,
                Type = type,
                Pattern = type == AppSecurityRuleTypes.Custom ? pattern : null,
                Replacement = replacement,
                Strategy = strategy,
                KeepLast = strategy == AppSecurityStrategies.Mask ? rule.KeepLast : null,
            });
        }

        return normalized;
    }
}
