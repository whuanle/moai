using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Settings.Commands;
using MoAI.Settings.Models;
using MoAI.Settings.Queries.Responses;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 设置领域服务.
/// </summary>
public class SettingsService : ISettingsService
{
    /// <summary>
    /// 网站名称最大长度（去首尾空白后）.
    /// </summary>
    public const int MaxSystemNameLength = 50;

    /// <summary>
    /// SMTP 服务器地址最大长度.
    /// </summary>
    public const int MaxSmtpHostLength = 255;

    /// <summary>
    /// SMTP 登录账号最大长度.
    /// </summary>
    public const int MaxSmtpUsernameLength = 320;

    /// <summary>
    /// SMTP 登录密码最大长度.
    /// </summary>
    public const int MaxSmtpPasswordLength = 200;

    /// <summary>
    /// 发件人显示名最大长度.
    /// </summary>
    public const int MaxSmtpFromNameLength = 100;

    /// <summary>
    /// 验证码邮件主题模板最大长度（与 EmailSenderService 主题上限一致）.
    /// </summary>
    public const int MaxEmailSubjectLength = 500;

    /// <summary>
    /// 验证码邮件正文模板最大长度（setting.value 列上限）.
    /// </summary>
    public const int MaxSettingValueLength = 2000;

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public SettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySettingsCommandResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var entities = await _databaseContext.Settings.ToListAsync(cancellationToken);
        var entityMap = entities.ToDictionary(e => e.Key);

        var items = SettingDefinitions.All
            .Select(definition =>
            {
                entityMap.TryGetValue(definition.Key, out var entity);
                return new SettingItemResponse
                {
                    Key = definition.Key,
                    Name = definition.Name,
                    Description = definition.Description,
                    Value = entity?.Value ?? definition.DefaultValue
                };
            })
            .ToArray();

        return new QuerySettingsCommandResponse
        {
            Items = items
        };
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> SaveSettingAsync(SaveSettingCommand command, CancellationToken cancellationToken)
    {
        var definition = SettingDefinitions.Find(command.Key);
        if (definition == null)
        {
            throw new BusinessException("无效的配置项.") { StatusCode = 400 };
        }

        var value = ValidateValue(command.Key, command.Value);
        await ValidateMemorySettingAsync(command.Key, value, cancellationToken);

        var entity = await _databaseContext.Settings.FirstOrDefaultAsync(s => s.Key == command.Key, cancellationToken);
        if (entity == null)
        {
            entity = new SettingEntity
            {
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                Value = value
            };
            await _databaseContext.Settings.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.Value = value;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 校验带格式约束的设置值（沙箱上限三项、网站名称、知识库文件类型、SMTP 邮件服务组），非法值直接拒绝，避免脏上限卡住全部应用的沙箱配置保存.
    /// 知识库文件类型返回归一后的值（小写去重），SMTP 开关/端口/加密方式与主机账号等返回归一后的值，其余原样返回.
    /// </summary>
    private static string ValidateValue(string key, string value)
    {
        switch (key)
        {
            case SettingDefinitions.SystemNameKey:
                if (value.Trim().Length > MaxSystemNameLength)
                {
                    throw new BusinessException($"网站名称长度不能超过 {MaxSystemNameLength} 个字符.") { StatusCode = 400 };
                }

                break;
            case SettingDefinitions.SandboxMaxTtlKey:
                if (!int.TryParse(value, out var ttl) || ttl < SandboxSettingsService.MinTtlSeconds || ttl > SandboxSettingsService.MaxTtlSeconds)
                {
                    throw new BusinessException($"沙箱存活时间上限必须是 {SandboxSettingsService.MinTtlSeconds}~{SandboxSettingsService.MaxTtlSeconds} 之间的整数（秒）.") { StatusCode = 400 };
                }

                break;

            case SettingDefinitions.SandboxMaxCpuKey:
                if (!SandboxQuantity.TryParseCpu(value, out var millicores) || millicores <= 0)
                {
                    throw new BusinessException("沙箱 CPU 上限格式无效，例如 4 或 2000m.") { StatusCode = 400 };
                }

                break;

            case SettingDefinitions.SandboxMaxMemoryKey:
                if (!SandboxQuantity.TryParseMemory(value, out var bytes) || bytes <= 0)
                {
                    throw new BusinessException("沙箱内存上限格式无效，例如 8Gi 或 512Mi.") { StatusCode = 400 };
                }

                break;

            case SettingDefinitions.WikiAllowedFileTypesKey:
                return string.Join(",", WikiFileTypes.NormalizeOrThrow(value));

            case SettingDefinitions.SmtpEnabledKey:
                var enabled = value.Trim().ToLowerInvariant();
                if (enabled != "true" && enabled != "false")
                {
                    throw new BusinessException("SMTP 邮件服务开关必须为 true 或 false.") { StatusCode = 400 };
                }

                return enabled;
            case SettingDefinitions.SmtpHostKey:
                var host = value.Trim();
                if (host.Length > MaxSmtpHostLength)
                {
                    throw new BusinessException($"SMTP 服务器地址长度不能超过 {MaxSmtpHostLength} 个字符.") { StatusCode = 400 };
                }

                return host;
            case SettingDefinitions.SmtpPortKey:
                if (!int.TryParse(value.Trim(), out var port) || port < 1 || port > 65535)
                {
                    throw new BusinessException("SMTP 端口必须是 1~65535 之间的整数.") { StatusCode = 400 };
                }

                return value.Trim();
            case SettingDefinitions.SmtpSecurityKey:
                var security = value.Trim().ToLowerInvariant();
                if (security != "ssl" && security != "starttls" && security != "none")
                {
                    throw new BusinessException("SMTP 加密方式必须是 ssl、starttls 或 none.") { StatusCode = 400 };
                }

                return security;
            case SettingDefinitions.SmtpUsernameKey:
                var username = value.Trim();
                if (username.Length > MaxSmtpUsernameLength)
                {
                    throw new BusinessException($"SMTP 登录账号长度不能超过 {MaxSmtpUsernameLength} 个字符.") { StatusCode = 400 };
                }

                return username;
            case SettingDefinitions.SmtpPasswordKey:
                if (value.Length > MaxSmtpPasswordLength)
                {
                    throw new BusinessException($"SMTP 登录密码长度不能超过 {MaxSmtpPasswordLength} 个字符.") { StatusCode = 400 };
                }

                return value;
            case SettingDefinitions.SmtpFromAddressKey:
                var fromAddress = value.Trim();
                if (fromAddress.Length == 0)
                {
                    return fromAddress;
                }

                if (fromAddress.Length > MaxSmtpUsernameLength || !System.Net.Mail.MailAddress.TryCreate(fromAddress, out _))
                {
                    throw new BusinessException("发件人地址不是合法的邮箱地址.") { StatusCode = 400 };
                }

                return fromAddress;
            case SettingDefinitions.SmtpFromNameKey:
                var fromName = value.Trim();
                if (fromName.Length > MaxSmtpFromNameLength)
                {
                    throw new BusinessException($"发件人显示名长度不能超过 {MaxSmtpFromNameLength} 个字符.") { StatusCode = 400 };
                }

                return fromName;
            case SettingDefinitions.RegisterEmailCodeEnabledKey:
                var codeEnabled = value.Trim().ToLowerInvariant();
                if (codeEnabled != "true" && codeEnabled != "false")
                {
                    throw new BusinessException("注册邮箱验证码开关必须为 true 或 false.") { StatusCode = 400 };
                }

                return codeEnabled;
            case SettingDefinitions.SmtpRegisterCodeSubjectKey:
            case SettingDefinitions.SmtpResetCodeSubjectKey:
                var subjectTemplate = value.Trim();
                if (subjectTemplate.Length > MaxEmailSubjectLength)
                {
                    throw new BusinessException($"验证码邮件主题长度不能超过 {MaxEmailSubjectLength} 个字符.") { StatusCode = 400 };
                }

                return subjectTemplate;
            case SettingDefinitions.SmtpRegisterCodeBodyKey:
            case SettingDefinitions.SmtpResetCodeBodyKey:
                if (value.Length > MaxSettingValueLength)
                {
                    throw new BusinessException($"验证码邮件正文长度不能超过 {MaxSettingValueLength} 个字符.") { StatusCode = 400 };
                }

                return value;
            case SettingDefinitions.MemoryEnabledKey:
                var memoryEnabled = value.Trim().ToLowerInvariant();
                if (memoryEnabled != "true" && memoryEnabled != "false")
                {
                    throw new BusinessException("长期记忆开关必须为 true 或 false.") { StatusCode = 400 };
                }

                return memoryEnabled;
            case SettingDefinitions.MemoryEmbeddingModelKey:
                var memoryModel = value.Trim();
                if (memoryModel.Length > 0 && !Guid.TryParse(memoryModel, out _))
                {
                    throw new BusinessException("记忆向量化模型 id 无效.") { StatusCode = 400 };
                }

                return memoryModel;
            case SettingDefinitions.MemoryEmbeddingDimensionsKey:
                if (!int.TryParse(value.Trim(), out var memoryDimensions) || memoryDimensions < 1 || memoryDimensions > 2000)
                {
                    throw new BusinessException("记忆向量维度必须是 1~2000 之间的整数.") { StatusCode = 400 };
                }

                return value.Trim();
        }

        return value;
    }

    /// <summary>
    /// 记忆设置联动校验：开启前须已选向量化模型；统一模型已配置且平台已有任意记忆（应用记忆或全局记忆）时模型与维度锁定（须先清空全部记忆）.
    /// 尚未配置统一模型（首次设置）不触发锁定——历史存量应用分片按直接切换策略处理，由管理员按需清空后按新口径重建.
    /// </summary>
    /// <param name="key">设置项 key.</param>
    /// <param name="value">归一后的新值.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    private async Task ValidateMemorySettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var isModelKey = key == SettingDefinitions.MemoryEmbeddingModelKey;
        var isDimensionsKey = key == SettingDefinitions.MemoryEmbeddingDimensionsKey;
        var isEnabledKey = key == SettingDefinitions.MemoryEnabledKey;

        if (isEnabledKey && value == "true")
        {
            var model = await ResolveStoredValueAsync(SettingDefinitions.MemoryEmbeddingModelKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new BusinessException("开启长期记忆前须先选择向量化模型.") { StatusCode = 400 };
            }
        }

        if (!isModelKey && !isDimensionsKey)
        {
            return;
        }

        var currentModel = await ResolveStoredValueAsync(SettingDefinitions.MemoryEmbeddingModelKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(currentModel))
        {
            return;
        }

        var current = await ResolveStoredValueAsync(key, cancellationToken);
        if (string.Equals(current.Trim(), value.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        // 统一口径后应用记忆与全局记忆共用同一模型/维度，任一记忆存在即锁定
        var hasMemories = await _databaseContext.AppMemoryShards.AsNoTracking()
            .AnyAsync(x => x.MemoryCount > 0, cancellationToken);
        if (hasMemories)
        {
            throw new BusinessException("平台已有记忆，向量化模型与维度不可更换，请先清空全部记忆.") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 读取设置项当前生效值（库值优先，缺失回退注册表默认值）.
    /// </summary>
    private async Task<string> ResolveStoredValueAsync(string key, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.Settings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        return entity != null && !string.IsNullOrWhiteSpace(entity.Value)
            ? entity.Value
            : SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;
    }
}
