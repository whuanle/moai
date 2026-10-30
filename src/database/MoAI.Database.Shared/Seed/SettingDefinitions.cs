using System;
using System.Collections.Generic;
using System.Linq;

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
    /// 组织默认角色（org_role 主键，空表示新注册用户无默认角色）.
    /// </summary>
    public const string OrgDefaultRoleKey = "ORG_DEFAULT_ROLE";

    /// <summary>
    /// 是否启用 SMTP 邮件服务（平台通知邮件发送总开关）.
    /// </summary>
    public const string SmtpEnabledKey = "SMTP_ENABLED";

    /// <summary>
    /// SMTP 服务器地址.
    /// </summary>
    public const string SmtpHostKey = "SMTP_HOST";

    /// <summary>
    /// SMTP 服务器端口.
    /// </summary>
    public const string SmtpPortKey = "SMTP_PORT";

    /// <summary>
    /// SMTP 加密方式（ssl / starttls / none）.
    /// </summary>
    public const string SmtpSecurityKey = "SMTP_SECURITY";

    /// <summary>
    /// SMTP 登录账号（中继服务器免认证时可留空）.
    /// </summary>
    public const string SmtpUsernameKey = "SMTP_USERNAME";

    /// <summary>
    /// SMTP 登录密码或授权码.
    /// </summary>
    public const string SmtpPasswordKey = "SMTP_PASSWORD";

    /// <summary>
    /// 发件人邮箱地址（留空取登录账号）.
    /// </summary>
    public const string SmtpFromAddressKey = "SMTP_FROM_ADDRESS";

    /// <summary>
    /// 发件人显示名.
    /// </summary>
    public const string SmtpFromNameKey = "SMTP_FROM_NAME";

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
    /// 注册是否需要邮箱验证码（需 SMTP 邮件服务已启用才生效）.
    /// </summary>
    public const string RegisterEmailCodeEnabledKey = "REGISTER_EMAIL_CODE_ENABLED";

    /// <summary>
    /// 注册验证码邮件主题模板（支持 {code} {siteName} {expireMinutes} 变量）.
    /// </summary>
    public const string SmtpRegisterCodeSubjectKey = "SMTP_REGISTER_CODE_SUBJECT";

    /// <summary>
    /// 注册验证码邮件正文模板（支持 {code} {siteName} {expireMinutes} 变量）.
    /// </summary>
    public const string SmtpRegisterCodeBodyKey = "SMTP_REGISTER_CODE_BODY";

    /// <summary>
    /// 找回密码验证码邮件主题模板（支持 {code} {siteName} {expireMinutes} 变量）.
    /// </summary>
    public const string SmtpResetCodeSubjectKey = "SMTP_RESET_CODE_SUBJECT";

    /// <summary>
    /// 找回密码验证码邮件正文模板（支持 {code} {siteName} {expireMinutes} 变量）.
    /// </summary>
    public const string SmtpResetCodeBodyKey = "SMTP_RESET_CODE_BODY";

    /// <summary>
    /// 注册验证码邮件主题默认模板.
    /// </summary>
    public const string DefaultRegisterCodeSubject = "{siteName} 注册验证码";

    /// <summary>
    /// 注册验证码邮件正文默认模板.
    /// </summary>
    public const string DefaultRegisterCodeBody = "你的注册验证码是 {code}，{expireMinutes} 分钟内有效。如非本人操作请忽略本邮件.";

    /// <summary>
    /// 找回密码验证码邮件主题默认模板.
    /// </summary>
    public const string DefaultResetCodeSubject = "{siteName} 找回密码验证码";

    /// <summary>
    /// 找回密码验证码邮件正文默认模板.
    /// </summary>
    public const string DefaultResetCodeBody = "你的找回密码验证码是 {code}，{expireMinutes} 分钟内有效。如非本人操作请忽略本邮件.";

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
            Description = "知识库允许上传的文档扩展名（逗号分隔，如 .pdf,.docx,.md），对网页端与开放接口上传同时生效；默认与文档/文本提取能力对齐，图片暂不支持.",
            DefaultValue = ".pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json"
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
            Key = OrgDefaultRoleKey,
            Name = "组织默认角色",
            Description = "新注册用户自动获得的角色（org_role 主键），留空表示新用户无默认角色；在组织角色页维护.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SmtpEnabledKey,
            Name = "SMTP 邮件服务",
            Description = "启用后，平台通知将通过 SMTP 服务发送邮件；不启用时无需填写服务器信息.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = SmtpHostKey,
            Name = "SMTP 服务器地址",
            Description = "SMTP 服务器域名或 IP，例如 smtp.exmail.qq.com.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SmtpPortKey,
            Name = "SMTP 端口",
            Description = "SMTP 服务器端口，1~65535；隐式 TLS 通常 465，STARTTLS 通常 587.",
            DefaultValue = "465"
        },
        new SettingDefinition
        {
            Key = SmtpSecurityKey,
            Name = "SMTP 加密方式",
            Description = "ssl（隐式 TLS，通常 465）/ starttls（通常 587）/ none（不加密，通常 25）.",
            DefaultValue = "ssl"
        },
        new SettingDefinition
        {
            Key = SmtpUsernameKey,
            Name = "SMTP 登录账号",
            Description = "SMTP 登录账号，内网中继服务器免认证时可留空.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SmtpPasswordKey,
            Name = "SMTP 登录密码",
            Description = "SMTP 登录密码或邮箱授权码.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SmtpFromAddressKey,
            Name = "发件人地址",
            Description = "通知邮件的发件人邮箱地址，留空时使用 SMTP 登录账号.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SmtpFromNameKey,
            Name = "发件人显示名",
            Description = "通知邮件的发件人显示名，例如 MoAI 通知.",
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
            Key = RegisterEmailCodeEnabledKey,
            Name = "注册需要邮箱验证码",
            Description = "开启后，新用户注册必须先获取邮箱验证码并校验通过才能完成注册；需 SMTP 邮件服务已启用才生效.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = SmtpRegisterCodeSubjectKey,
            Name = "注册验证码邮件主题",
            Description = "注册验证码邮件主题模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.",
            DefaultValue = DefaultRegisterCodeSubject
        },
        new SettingDefinition
        {
            Key = SmtpRegisterCodeBodyKey,
            Name = "注册验证码邮件正文",
            Description = "注册验证码邮件正文模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.",
            DefaultValue = DefaultRegisterCodeBody
        },
        new SettingDefinition
        {
            Key = SmtpResetCodeSubjectKey,
            Name = "找回密码验证码邮件主题",
            Description = "找回密码验证码邮件主题模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.",
            DefaultValue = DefaultResetCodeSubject
        },
        new SettingDefinition
        {
            Key = SmtpResetCodeBodyKey,
            Name = "找回密码验证码邮件正文",
            Description = "找回密码验证码邮件正文模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.",
            DefaultValue = DefaultResetCodeBody
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
