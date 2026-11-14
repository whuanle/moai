using System;
using System.Collections.Generic;
using System.Linq;
using MoAI.Infra;

namespace MoAI.Database.Seed;

/// <summary>
/// 内置设置项注册表，作为种子数据与首次写入时的初始数据来源.
/// </summary>
public static class SettingDefinitions
{
    /// <summary>
    /// 是否开启知识图谱图数据库.
    /// </summary>
    public const string GraphEnabledKey = "KG_ENABLED";

    /// <summary>
    /// 图数据库连接地址.
    /// </summary>
    public const string GraphUriKey = "KG_URI";

    /// <summary>
    /// 图数据库登录用户名.
    /// </summary>
    public const string GraphUsernameKey = "KG_USERNAME";

    /// <summary>
    /// 图数据库登录密码.
    /// </summary>
    public const string GraphPasswordKey = "KG_PASSWORD";

    /// <summary>
    /// 图数据库方言（memgraph / neo4j）.
    /// </summary>
    public const string GraphDialectKey = "KG_DIALECT";

    /// <summary>
    /// 知识库上传文件大小上限（MB），0 表示不限制.
    /// </summary>
    public const string WikiMaxFileSizeKey = "WIKI_MAX_FILE_SIZE_MB";

    /// <summary>
    /// 知识库允许上传的文件类型（逗号分隔扩展名，如 .pdf,.md）.
    /// </summary>
    public const string WikiAllowedFileTypesKey = "WIKI_ALLOWED_FILE_TYPES";

    /// <summary>
    /// 每个应用沙箱最大存活时间（秒）.
    /// </summary>
    public const string SandboxMaxTtlKey = "SANDBOX_MAX_TTL_SECONDS";

    /// <summary>
    /// 每个应用沙箱 CPU 限制上限（K8s 数量格式，如 4 或 2000m）.
    /// </summary>
    public const string SandboxMaxCpuKey = "SANDBOX_MAX_CPU";

    /// <summary>
    /// 每个应用沙箱内存限制上限（K8s 数量格式，如 8Gi 或 512Mi）.
    /// </summary>
    public const string SandboxMaxMemoryKey = "SANDBOX_MAX_MEMORY";

    /// <summary>
    /// 沙箱出站网络管控模式：app（应用自定义）/ deny（强制默认拒绝）/ intranet（默认拒绝并放行内网网段）.
    /// </summary>
    public const string SandboxNetworkModeKey = "SANDBOX_NETWORK_MODE";

    /// <summary>
    /// 沙箱内网网段（逗号分隔 IP / CIDR），intranet 模式下自动放行.
    /// </summary>
    public const string SandboxIntranetCidrsKey = "SANDBOX_INTRANET_CIDRS";

    /// <summary>
    /// 沙箱禁用命令清单（换行分隔模式，全站强制），拦截 sandbox_run_shell / sandbox_run_code.
    /// </summary>
    public const string SandboxDeniedCommandsKey = "SANDBOX_DENIED_COMMANDS";

    /// <summary>
    /// 网站 Logo（存储中的 ObjectKey，空表示使用默认 Logo）.
    /// </summary>
    public const string SystemLogoKey = "SYSTEM_LOGO";

    /// <summary>
    /// 网站名称（仅前端展示），空表示使用配置文件中的默认名称.
    /// </summary>
    public const string SystemNameKey = "SYSTEM_NAME";

    /// <summary>
    /// 登录页背景图（存储中的 ObjectKey，空表示使用前端默认背景）.
    /// </summary>
    public const string LoginBackgroundKey = "LOGIN_BACKGROUND";

    /// <summary>
    /// 是否启用平台邮件发送（总开关）：需在通知中心配置 SMTP 渠道并在此绑定后才生效.
    /// </summary>
    public const string SmtpEnabledKey = "SMTP_ENABLED";

    /// <summary>
    /// 绑定的通知中心 SMTP 渠道（notification_channel.id，空表示未绑定；SMTP 连接配置统一在通知中心维护）.
    /// </summary>
    public const string SmtpChannelIdKey = "SMTP_CHANNEL_ID";

    /// <summary>
    /// 是否开启长期记忆（总开关）：应用记忆与个人全局记忆（跨应用记忆）统一受此管控，关闭时应用不可开启记忆.
    /// </summary>
    public const string MemoryEnabledKey = "MEMORY_ENABLED";

    /// <summary>
    /// 记忆统一向量化模型（ai_model.id，须为公开启用中的 embedding 模型）：应用记忆与全局记忆共用，写入任意记忆后锁定.
    /// </summary>
    public const string MemoryEmbeddingModelKey = "MEMORY_EMBEDDING_MODEL";

    /// <summary>
    /// 记忆统一向量维度（1~2000，默认 1024）：与向量化模型一起在写入任意记忆后锁定.
    /// </summary>
    public const string MemoryEmbeddingDimensionsKey = "MEMORY_EMBEDDING_DIMENSIONS";

    /// <summary>
    /// 是否开启跨应用记忆：管控应用可否允许用户接入个人跨应用记忆（全局记忆），开启须先开启长期记忆总开关.
    /// </summary>
    public const string MemoryGlobalEnabledKey = "MEMORY_GLOBAL_ENABLED";

    /// <summary>
    /// 注册是否需要邮箱验证码（需 SMTP 邮件服务已启用才生效）.
    /// </summary>
    public const string RegisterEmailCodeEnabledKey = "REGISTER_EMAIL_CODE_ENABLED";

    /// <summary>
    /// 注册验证码绑定的通知中心模板 id（notification_template.id，留空使用内置模板）.
    /// </summary>
    public const string SmtpRegisterCodeTemplateIdKey = "SMTP_REGISTER_CODE_TEMPLATE_ID";

    /// <summary>
    /// 找回密码验证码绑定的通知中心模板 id（notification_template.id，留空使用内置模板）.
    /// </summary>
    public const string SmtpResetCodeTemplateIdKey = "SMTP_RESET_CODE_TEMPLATE_ID";

    /// <summary>
    /// 资源使用统计落库粒度（day=按天 / hour=按小时），数据量由管理员权衡.
    /// </summary>
    public const string StatisticsGranularityKey = "STATISTICS_GRANULARITY";

    /// <summary>
    /// 是否强制全员开启两步验证：开启后未绑定 2FA 的用户登录只能进入个人设置绑定页，绑定后才能使用系统.
    /// </summary>
    public const string LoginForced2faKey = "LOGIN_FORCED_2FA";

    /// <summary>
    /// 登录邮箱验证码策略（off=关闭 / always=始终需要 / after_fail=密码错误一次后需要），需 SMTP 邮件服务已启用才生效.
    /// </summary>
    public const string LoginEmailCodeModeKey = "LOGIN_EMAIL_CODE_MODE";

    /// <summary>
    /// 图形验证码策略（off=关闭 / always=始终需要 / after_fail=出现风险行为后需要），覆盖登录与注册/找回密码发码.
    /// </summary>
    public const string LoginCaptchaModeKey = "LOGIN_CAPTCHA_MODE";

    /// <summary>
    /// 邮箱验证码 after_fail 触发阈值：同一用户名累计登录失败达到该次数后的登录需要邮箱验证码（分级递进，图形验证码 fail≥1 先行）.
    /// </summary>
    public const string LoginEmailCodeFailThresholdKey = "LOGIN_EMAIL_CODE_FAIL_THRESHOLD";

    /// <summary>
    /// 登录验证码绑定的通知中心模板 id（notification_template.id，留空使用内置模板）.
    /// </summary>
    public const string SmtpLoginCodeTemplateIdKey = "SMTP_LOGIN_CODE_TEMPLATE_ID";

    /// <summary>
    /// 登录安全风控总开关：关闭时仅记录登录日志，不执行风险判定与处置.
    /// </summary>
    public const string LoginSafetyEnabledKey = "LS_ENABLED";

    /// <summary>
    /// 高风险事件推送的通知渠道 ID 列表（逗号分隔 notification_channel.id，留空不推送渠道）.
    /// </summary>
    public const string LoginSafetyNotifyChannelIdsKey = "LS_NOTIFY_CHANNEL_IDS";

    /// <summary>
    /// 高风险事件渠道告警的收件人邮箱列表（逗号分隔，渠道含 SMTP 邮件渠道时必填）.
    /// </summary>
    public const string LoginSafetyNotifyEmailsKey = "LS_NOTIFY_EMAILS";

    /// <summary>
    /// 同一 IP 登录失败次数封禁阈值（滑动窗口内，默认 20）.
    /// </summary>
    public const string LoginSafetyIpFailThresholdKey = "LS_IP_FAIL_THRESHOLD";

    /// <summary>
    /// IP 失败计数滑动窗口（分钟，默认 10）.
    /// </summary>
    public const string LoginSafetyIpFailWindowMinutesKey = "LS_IP_FAIL_WINDOW_MINUTES";

    /// <summary>
    /// IP 封禁时长（分钟，默认 30）.
    /// </summary>
    public const string LoginSafetyIpBanMinutesKey = "LS_IP_BAN_MINUTES";

    /// <summary>
    /// 同一 IP 撞库喷洒阈值：窗口内对多少个不同用户名登录失败后封禁（默认 5）.
    /// </summary>
    public const string LoginSafetyDistinctUserThresholdKey = "LS_DISTINCT_USER_THRESHOLD";

    /// <summary>
    /// 新 IP 判定回看天数：该用户近 N 天无此 IP 成功登录视为新 IP（默认 90）.
    /// </summary>
    public const string LoginSafetyNewIpDaysKey = "LS_NEW_IP_DAYS";

    /// <summary>
    /// 疾速旅行判定窗口（分钟）：与上次成功登录属地不同且间隔小于该值（默认 120）.
    /// </summary>
    public const string LoginSafetyTravelWindowMinutesKey = "LS_TRAVEL_WINDOW_MINUTES";

    /// <summary>
    /// 注册验证码邮件主题兜底模板（通知中心内置模板缺失或渲染失败时使用）.
    /// </summary>
    public const string DefaultRegisterCodeSubject = "{siteName} 注册验证码";

    /// <summary>
    /// 注册验证码邮件正文兜底模板（通知中心内置模板缺失或渲染失败时使用）.
    /// </summary>
    public const string DefaultRegisterCodeBody = "你的注册验证码是 {code}，{expireMinutes} 分钟内有效。如非本人操作请忽略本邮件.";

    /// <summary>
    /// 找回密码验证码邮件主题兜底模板（通知中心内置模板缺失或渲染失败时使用）.
    /// </summary>
    public const string DefaultResetCodeSubject = "{siteName} 找回密码验证码";

    /// <summary>
    /// 找回密码验证码邮件正文兜底模板（通知中心内置模板缺失或渲染失败时使用）.
    /// </summary>
    public const string DefaultResetCodeBody = "你的找回密码验证码是 {code}，{expireMinutes} 分钟内有效。如非本人操作请忽略本邮件.";

    /// <summary>
    /// 登录验证码邮件主题兜底模板（通知中心内置模板缺失或渲染失败时使用）.
    /// </summary>
    public const string DefaultLoginCodeSubject = "{siteName} 登录验证码";

    /// <summary>
    /// 登录验证码邮件正文兜底模板（通知中心内置模板缺失或渲染失败时使用）.
    /// </summary>
    public const string DefaultLoginCodeBody = "你的登录验证码是 {code}，{expireMinutes} 分钟内有效。如非本人操作请忽略本邮件.";

    private static readonly List<SettingDefinition> BackingField = new()
    {
        new SettingDefinition
        {
            Key = GraphEnabledKey,
            Name = "知识图谱",
            Description = "开启后，团队可以使用知识图谱能力；关闭时无需填写连接信息.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = GraphUriKey,
            Name = "图数据库连接地址",
            Description = "图数据库 Bolt 连接地址，例如 neo4j://127.0.0.1:7687 或 bolt://127.0.0.1:7687.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = GraphUsernameKey,
            Name = "图数据库用户名",
            Description = "图数据库登录用户名.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = GraphPasswordKey,
            Name = "图数据库密码",
            Description = "图数据库登录密码.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = GraphDialectKey,
            Name = "图数据库方言",
            Description = "memgraph 或 neo4j，影响内省与索引语句；外部接入 Neo4j 实例时选 neo4j.",
            DefaultValue = "memgraph"
        },
        new SettingDefinition
        {
            Key = WikiMaxFileSizeKey,
            Name = "知识库最大文件大小",
            Description = "知识库上传文档的大小上限（MB），默认 50，0 表示不限制（平台硬上限 1GB）.",
            DefaultValue = "50"
        },
        new SettingDefinition
        {
            Key = WikiAllowedFileTypesKey,
            Name = "知识库允许上传的文件类型",
            Description = "知识库允许上传的文档扩展名（逗号分隔，如 .pdf,.docx,.md），对网页端与开放接口上传同时生效；默认与文档/文本提取能力对齐（Office/PDF/HTML/Markdown/EPUB/ODT/RTF 与代码配置类文本等 42 种），图片暂不支持.",
            DefaultValue = DocumentFormatConst.ExtractableFormatsJoined
        },
        new SettingDefinition
        {
            Key = SandboxMaxTtlKey,
            Name = "沙箱存活时间上限",
            Description = "每个应用沙箱最大存活时间（秒），团队保存应用配置时不得超出；默认 86400（24 小时），范围 60~604800.",
            DefaultValue = "86400"
        },
        new SettingDefinition
        {
            Key = SandboxMaxCpuKey,
            Name = "沙箱 CPU 上限",
            Description = "每个应用沙箱 CPU 限制上限（K8s 数量格式，如 4 或 2000m），团队保存应用配置时不得超出；默认 4.",
            DefaultValue = "4"
        },
        new SettingDefinition
        {
            Key = SandboxMaxMemoryKey,
            Name = "沙箱内存上限",
            Description = "每个应用沙箱内存限制上限（K8s 数量格式，如 8Gi 或 512Mi），团队保存应用配置时不得超出；默认 8Gi.",
            DefaultValue = "8Gi"
        },
        new SettingDefinition
        {
            Key = SandboxNetworkModeKey,
            Name = "沙箱出站网络管控模式",
            Description = "app=应用自定义出站策略；deny=强制默认拒绝（应用仅可追加放行规则）；intranet=默认拒绝并自动放行内网网段（应用可追加白名单）.",
            DefaultValue = "app"
        },
        new SettingDefinition
        {
            Key = SandboxIntranetCidrsKey,
            Name = "沙箱内网网段",
            Description = "逗号分隔的 IP / CIDR 网段，intranet 管控模式下自动放行；默认覆盖三类私有地址段.",
            DefaultValue = "10.0.0.0/8,172.16.0.0/12,192.168.0.0/16"
        },
        new SettingDefinition
        {
            Key = SandboxDeniedCommandsKey,
            Name = "沙箱禁用命令清单",
            Description = "换行分隔的禁用命令模式，全站强制（与应用级清单取并集），命中即拦截 sandbox_run_shell / sandbox_run_code；模式默认按全词匹配（rm 不误杀 confirm），regex: 前缀为原生正则；留空表示不启用平台级禁用清单.",
            DefaultValue = "shutdown\nreboot\npoweroff\nhalt\ninit\nmkfs\ndd\nfdisk\nparted\niptables\nufw\nuseradd\nuserdel\nusermod\npasswd\nvisudo\ncrontab\ninsmod"
        },
        new SettingDefinition
        {
            Key = SystemLogoKey,
            Name = "网站 Logo",
            Description = "上传后替换全局网站 Logo（侧边栏与登录/注册页），留空使用默认 Logo.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SystemNameKey,
            Name = "网站名称",
            Description = "仅影响前端展示（侧边栏标题、登录/注册页与浏览器标签页），留空使用配置文件默认名称.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = LoginBackgroundKey,
            Name = "登录页背景图",
            Description = "上传后替换登录页整页背景，留空使用前端默认背景.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SmtpEnabledKey,
            Name = "邮箱验证",
            Description = "启用后，平台邮件（注册/找回密码/登录验证码、安全告警等）通过绑定的 SMTP 服务发送；SMTP 连接配置统一在通知中心维护，此处仅绑定使用.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = SmtpChannelIdKey,
            Name = "绑定 SMTP 服务",
            Description = "绑定的通知中心 SMTP 渠道 id（notification_channel.id），留空表示未绑定；渠道连接配置在通知中心维护.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = MemoryEnabledKey,
            Name = "长期记忆",
            Description = "长期记忆总开关：开启后应用可开启记忆，用户可在对话中维护应用内记忆与个人跨应用记忆（全局记忆）；关闭时应用不可开启记忆.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = MemoryEmbeddingModelKey,
            Name = "记忆向量化模型",
            Description = "应用记忆与全局记忆统一使用的向量化模型 id（公开模型），写入任意记忆后锁定，更换需先清空全部记忆.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = MemoryEmbeddingDimensionsKey,
            Name = "记忆向量维度",
            Description = "记忆统一向量维度（1~2000），默认 1024；与向量化模型一起在写入任意记忆后锁定.",
            DefaultValue = "1024"
        },
        new SettingDefinition
        {
            Key = MemoryGlobalEnabledKey,
            Name = "跨应用记忆",
            Description = "是否开启跨应用记忆：开启后应用才可允许用户接入个人跨应用记忆（全局记忆）；关闭时所有应用的用户跨应用记忆接入停用，应用内记忆不受影响.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = RegisterEmailCodeEnabledKey,
            Name = "注册需要邮箱验证码",
            Description = "开启后，新用户注册必须先获取邮箱验证码并校验通过才能完成注册；需 SMTP 邮件服务已启用才生效.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = SmtpRegisterCodeTemplateIdKey,
            Name = "注册验证码通知模板",
            Description = "绑定的通知中心模板 id（notification_template.id），留空使用平台内置的注册验证码模板；模板内容与渲染测试在通知中心维护.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SmtpResetCodeTemplateIdKey,
            Name = "找回密码验证码通知模板",
            Description = "绑定的通知中心模板 id（notification_template.id），留空使用平台内置的找回密码验证码模板；模板内容与渲染测试在通知中心维护.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = StatisticsGranularityKey,
            Name = "统计粒度",
            Description = "资源使用统计的落库粒度：day=按天（默认，数据量小）/ hour=按小时（更细，数据量约 24 倍）；只影响统一统计日表，资源自身 use_count 不受影响.",
            DefaultValue = "day"
        },
        new SettingDefinition
        {
            Key = LoginForced2faKey,
            Name = "强制两步验证",
            Description = "开启后，未绑定 2FA 的用户登录时强制进入个人设置绑定两步验证，绑定完成后才能使用系统.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = LoginEmailCodeModeKey,
            Name = "登录邮箱验证码策略",
            Description = "off=关闭（默认）/ always=每次登录都需要邮箱验证码 / after_fail=密码错误达到阈值后的登录需要邮箱验证码；需 SMTP 邮件服务已启用才生效.",
            DefaultValue = "off"
        },
        new SettingDefinition
        {
            Key = LoginCaptchaModeKey,
            Name = "图形验证码策略",
            Description = "off=关闭（默认）/ always=始终需要 / after_fail=出现风险行为后需要（登录：用户名或 IP 有登录失败记录；注册/找回：该 IP 已发过一次验证码）；覆盖登录提交与注册/找回密码的发码，本地生成不依赖外部服务.",
            DefaultValue = "off"
        },
        new SettingDefinition
        {
            Key = LoginEmailCodeFailThresholdKey,
            Name = "邮箱验证码触发失败次数",
            Description = "邮箱验证码策略为 after_fail 时，同一用户名累计登录失败达到该次数后的登录才需要邮箱验证码（fail≥1 先出现图形验证码，分级递进）；默认 3.",
            DefaultValue = "3"
        },
        new SettingDefinition
        {
            Key = SmtpLoginCodeTemplateIdKey,
            Name = "登录验证码通知模板",
            Description = "绑定的通知中心模板 id（notification_template.id），留空使用平台内置的登录验证码模板；模板内容与渲染测试在通知中心维护.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = LoginSafetyEnabledKey,
            Name = "登录安全风控",
            Description = "登录风控总开关：开启后记录登录日志并执行风险判定（新 IP/异地/疾速旅行/IP 爆破/撞库喷洒）；关闭时仅记录登录日志.",
            DefaultValue = "true"
        },
        new SettingDefinition
        {
            Key = LoginSafetyNotifyChannelIdsKey,
            Name = "风险事件通知渠道",
            Description = "高风险事件推送的通知中心渠道 ID（逗号分隔）；留空不推送渠道，站内信始终推送给受影响用户.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = LoginSafetyNotifyEmailsKey,
            Name = "风险事件告警收件人",
            Description = "高风险事件渠道告警的收件人邮箱（逗号分隔）；投递渠道含 SMTP 邮件渠道时必填.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = LoginSafetyIpFailThresholdKey,
            Name = "IP 失败封禁阈值",
            Description = "滑动窗口内同一 IP 登录失败达到该次数后自动封禁该 IP；默认 20.",
            DefaultValue = "20"
        },
        new SettingDefinition
        {
            Key = LoginSafetyIpFailWindowMinutesKey,
            Name = "IP 失败计数窗口",
            Description = "IP 登录失败计数的滑动窗口长度（分钟）；默认 10.",
            DefaultValue = "10"
        },
        new SettingDefinition
        {
            Key = LoginSafetyIpBanMinutesKey,
            Name = "IP 封禁时长",
            Description = "触发自动封禁后的封禁时长（分钟）；默认 30，管理员可在日志事件页提前解封.",
            DefaultValue = "30"
        },
        new SettingDefinition
        {
            Key = LoginSafetyDistinctUserThresholdKey,
            Name = "撞库喷洒阈值",
            Description = "滑动窗口内同一 IP 对多少个不同用户名登录失败后判定为撞库并封禁；默认 5.",
            DefaultValue = "5"
        },
        new SettingDefinition
        {
            Key = LoginSafetyNewIpDaysKey,
            Name = "新 IP 回看天数",
            Description = "该用户近 N 天内无此 IP 成功登录记录则视为新 IP 登录（中风险）；默认 90，0 表示关闭新 IP 检测.",
            DefaultValue = "90"
        },
        new SettingDefinition
        {
            Key = LoginSafetyTravelWindowMinutesKey,
            Name = "疾速旅行窗口",
            Description = "与上次成功登录属地不同且间隔小于该值（分钟）判定为疾速旅行（高风险）；默认 120.",
            DefaultValue = "120"
        }
    };

    /// <summary>
    /// 全部内置设置项.
    /// </summary>
    public static IReadOnlyList<SettingDefinition> All => BackingField;

    /// <summary>
    /// 根据 key 查找设置项，未找到返回 null.
    /// </summary>
    /// <param name="key">设置项 key.</param>
    /// <returns>返回 <see cref="SettingDefinition"/> 或 null.</returns>
    public static SettingDefinition? Find(string key)
        => BackingField.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
