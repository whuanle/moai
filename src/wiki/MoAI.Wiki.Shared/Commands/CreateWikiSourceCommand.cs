using System;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 创建知识库外部源，需要团队 Admin 及以上角色.
/// 飞书文档源支持两种绑定方式：选择团队已有的飞书应用连接（<see cref="FeishuAppId"/>），
/// 或直接填写飞书开放平台 AppID/AppSecret 新建连接（<see cref="NewAppId"/>/<see cref="NewAppSecret"/>）.
/// 创建后自动绑定飞书渠道（订阅型渠道，可与团队应用共享同一飞书应用）.
/// </summary>
public class CreateWikiSourceCommand : IRequest<SimpleGuid>, IModelValidator<CreateWikiSourceCommand>, IUserIdContext, IWikiSourceWorkflowCommand, IWikiCrawlerSourceCommand, IWikiFeishuSourceCommand
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 外部源类型.
    /// </summary>
    public WikiSourceType SourceType { get; init; }

    /// <summary>
    /// 外部源名称，知识库内唯一.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 爬虫源配置（<see cref="SourceType"/> 为 <see cref="WikiSourceType.Crawler"/> 时必填）.
    /// </summary>
    public WikiSourceCrawlerConfig? Crawler { get; init; }

    /// <summary>
    /// 方式一：选择团队已有的飞书应用连接 id（feishu_app.id）.
    /// </summary>
    public Guid? FeishuAppId { get; init; }

    /// <summary>
    /// 方式二：新建飞书应用连接的名称.
    /// </summary>
    public string? NewAppName { get; init; }

    /// <summary>
    /// 方式二：飞书开放平台 AppID，形如 cli_xxx.
    /// </summary>
    public string? NewAppId { get; init; }

    /// <summary>
    /// 方式二：飞书开放平台 AppSecret.
    /// </summary>
    public string? NewAppSecret { get; init; }

    /// <summary>
    /// 方式二：接入域名，为空默认 https://open.feishu.cn；Lark 为 https://open.larksuite.com.
    /// </summary>
    public string? NewAppDomain { get; init; }

    /// <summary>
    /// 飞书知识空间节点 token.
    /// </summary>
    public string NodeToken { get; init; } = string.Empty;

    /// <summary>
    /// 是否拉取节点下的全部子文档.
    /// </summary>
    public bool IncludeSubNodes { get; init; } = true;

    /// <summary>
    /// 子节点遍历最大深度，0 表示不限.
    /// </summary>
    public int MaxDepth { get; init; }

    /// <summary>
    /// 单次同步文档数量上限，0 表示取默认值.
    /// </summary>
    public int MaxDocuments { get; init; }

    /// <summary>
    /// 外部源工作流配置（切割/元数据/向量化三步），为空表示回退知识库默认工作流.
    /// </summary>
    public WikiWorkflowConfig? Workflow { get; init; }

    /// <summary>
    /// 定时同步 cron 表达式（UTC），为空表示不开启定时同步.
    /// </summary>
    public string? Cron { get; init; }

    /// <summary>
    /// 是否开启飞书事件订阅，开启后文档变更事件到达即触发重新拉取.
    /// </summary>
    public bool IsEventSubscription { get; init; }

    /// <summary>
    /// 是否启用该外部源.
    /// </summary>
    public bool IsEnable { get; init; } = true;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateWikiSourceCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("请输入外部源名称.")
            .MaximumLength(50).WithMessage("外部源名称不能超过 50 个字符.");
        validate.RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("描述不能超过 255 个字符.");
        validate.RuleFor(x => x.SourceType)
            .Must(x => x is WikiSourceType.FeishuDoc or WikiSourceType.Crawler)
            .WithMessage("暂不支持该外部源类型.");
        validate.RuleFor(x => x.Cron)
            .Must(x => string.IsNullOrWhiteSpace(x) || WikiSourceDefaults.IsValidCron(x))
            .WithMessage("定时同步 cron 表达式不正确.");

        ValidateCrawler(validate);
        ValidateFeishu(validate);

        validate.RuleFor(x => x.MaxDepth)
            .InclusiveBetween(0, WikiSourceDefaults.MaxDepthLimit)
            .WithMessage($"遍历深度不能超过 {WikiSourceDefaults.MaxDepthLimit}.");
        validate.RuleFor(x => x.MaxDocuments)
            .InclusiveBetween(0, WikiSourceDefaults.MaxDocumentsLimit)
            .WithMessage($"单次同步文档数不能超过 {WikiSourceDefaults.MaxDocumentsLimit}.");

        ValidateWorkflow(validate);
    }

    /// <summary>
    /// 爬虫源配置校验，仅在类型为爬虫时生效.
    /// </summary>
    /// <typeparam name="T">命令类型.</typeparam>
    /// <param name="validate">校验器.</param>
    internal static void ValidateCrawler<T>(AbstractValidator<T> validate)
        where T : IWikiCrawlerSourceCommand
    {
        validate.RuleFor(x => x.Crawler)
            .NotNull().WithMessage("请填写网页爬虫配置.")
            .When(x => x.SourceType == WikiSourceType.Crawler);
        validate.RuleFor(x => x.Crawler!.StartUrl)
            .Must(x => WikiSourceDefaults.IsValidCrawlerUrl(x))
            .WithMessage($"请输入合法的起始 URL（http/https，不超过 {WikiSourceDefaults.CrawlerUrlMaxLength} 个字符）.")
            .When(x => x.SourceType == WikiSourceType.Crawler && x.Crawler != null);
        validate.RuleFor(x => x.Crawler!.PathPrefix)
            .MaximumLength(WikiSourceDefaults.CrawlerUrlMaxLength)
            .WithMessage($"路径前缀不能超过 {WikiSourceDefaults.CrawlerUrlMaxLength} 个字符.")
            .When(x => x.Crawler?.PathPrefix != null);
        validate.RuleFor(x => x.Crawler!.MaxDepth)
            .InclusiveBetween(0, WikiSourceDefaults.CrawlerMaxDepthLimit)
            .WithMessage($"爬取深度不能超过 {WikiSourceDefaults.CrawlerMaxDepthLimit}.")
            .When(x => x.Crawler != null);
        validate.RuleFor(x => x.Crawler!.MaxPages)
            .InclusiveBetween(0, WikiSourceDefaults.CrawlerMaxPagesLimit)
            .WithMessage($"单轮爬取页数不能超过 {WikiSourceDefaults.CrawlerMaxPagesLimit}.")
            .When(x => x.Crawler != null);
        validate.RuleFor(x => x.Crawler!.RequestIntervalSeconds)
            .InclusiveBetween(0, WikiSourceDefaults.MaxCrawlerRequestIntervalSeconds)
            .WithMessage($"抓取间隔不能超过 {WikiSourceDefaults.MaxCrawlerRequestIntervalSeconds} 秒.")
            .When(x => x.Crawler != null);
        validate.RuleFor(x => x.Crawler!.TimeoutSeconds)
            .InclusiveBetween(0, WikiSourceDefaults.MaxCrawlerTimeoutSeconds)
            .WithMessage($"请求超时不能超过 {WikiSourceDefaults.MaxCrawlerTimeoutSeconds} 秒.")
            .When(x => x.Crawler != null);
        // UserAgent/正文选择器按「提交了多少字符」校验，与是否为爬虫源无关；未提交配置时跳过，避免空引用
        validate.RuleFor(x => x.Crawler!.UserAgent)
            .MaximumLength(255).WithMessage("UserAgent 不能超过 255 个字符.")
            .When(x => x.Crawler != null);
        validate.RuleFor(x => x.Crawler!.ContentSelector)
            .MaximumLength(WikiSourceDefaults.CrawlerSelectorMaxLength)
            .WithMessage($"正文选择器不能超过 {WikiSourceDefaults.CrawlerSelectorMaxLength} 个字符.")
            .When(x => x.Crawler != null);
    }

    /// <summary>
    /// 飞书文档源配置校验，仅在类型为飞书文档时生效.
    /// </summary>
    /// <typeparam name="T">命令类型.</typeparam>
    /// <param name="validate">校验器.</param>
    internal static void ValidateFeishu<T>(AbstractValidator<T> validate)
        where T : IWikiFeishuSourceCommand
    {
        validate.RuleFor(x => x.NodeToken)
            .NotEmpty().WithMessage("请输入飞书文档节点 token.")
            .MaximumLength(128).WithMessage("节点 token 不能超过 128 个字符.")
            .When(x => x.SourceType == WikiSourceType.FeishuDoc);

        // 两种绑定方式互斥且必选其一（仅飞书文档源需要）
        validate.RuleFor(x => x)
            .Must(x => x.SourceType != WikiSourceType.FeishuDoc
                || x.FeishuAppId.HasValue
                || (!string.IsNullOrWhiteSpace(x.NewAppId) && !string.IsNullOrWhiteSpace(x.NewAppSecret)))
            .WithMessage("请选择已有飞书应用连接，或填写飞书开放平台 AppID 与 AppSecret 新建连接.");
        validate.RuleFor(x => x)
            .Must(x => !x.FeishuAppId.HasValue || string.IsNullOrWhiteSpace(x.NewAppId))
            .WithMessage("选择已有连接与新建连接只能二选一.");
        validate.RuleFor(x => x.NewAppName)
            .NotEmpty().WithMessage("请输入飞书应用连接名称.")
            .MaximumLength(50).WithMessage("连接名称不能超过 50 个字符.")
            .When(x => !string.IsNullOrWhiteSpace(x.NewAppId));
        validate.RuleFor(x => x.NewAppId)
            .MaximumLength(64).WithMessage("飞书 AppID 不能超过 64 个字符.");
        validate.RuleFor(x => x.NewAppSecret)
            .MaximumLength(128).WithMessage("飞书 AppSecret 不能超过 128 个字符.");
        validate.RuleFor(x => x.NewAppDomain)
            .MaximumLength(100).WithMessage("接入域名不能超过 100 个字符.");
    }

    /// <summary>
    /// 外部源工作流配置校验，与知识库默认工作流口径一致（见 UpdateWikiWorkflowCommand）.
    /// </summary>
    /// <typeparam name="T">命令类型.</typeparam>
    /// <param name="validate">校验器.</param>
    internal static void ValidateWorkflow<T>(AbstractValidator<T> validate)
        where T : IWikiSourceWorkflowCommand
    {
        validate.RuleFor(x => x.Workflow)
            .Must(x => x?.Partition == null || x.Partition.Mode != WorkflowPartitionMode.Ai || x.Partition.AiModelId != Guid.Empty)
            .WithMessage("请选择用于智能切割的对话模型.");
        validate.RuleFor(x => x.Workflow)
            .Must(x => x?.Partition == null || x.Partition.Mode == WorkflowPartitionMode.Ai || (x.Partition.ChunkSize > 0 && x.Partition.ChunkSize <= 8192))
            .WithMessage("切片大小必须大于 0 且不超过 8192.");
        validate.RuleFor(x => x.Workflow)
            .Must(x => x?.Partition == null || x.Partition.Mode == WorkflowPartitionMode.Ai || (x.Partition.ChunkOverlap >= 0 && x.Partition.ChunkOverlap <= 8192))
            .WithMessage("切片重叠不能小于 0 且不超过 8192.");
        validate.RuleFor(x => x.Workflow)
            .Must(x => x?.Partition == null || x.Partition.Mode == WorkflowPartitionMode.Ai
                || x.Partition.OverlapUnit != DocumentPartitionOverlapUnit.Character
                || x.Partition.ChunkOverlap < x.Partition.ChunkSize)
            .WithMessage("按字符重叠时，切片重叠必须小于切片大小.");
        validate.RuleFor(x => x.Workflow)
            .Must(x => x?.Partition == null || string.IsNullOrEmpty(x.Partition.TokenEncodingOrModel) || x.Partition.TokenEncodingOrModel.Length <= 64)
            .WithMessage("Token 编码或模型名不能超过 64 个字符.");
        validate.RuleFor(x => x.Workflow)
            .Must(x => x?.Metadata == null || x.Metadata.MetadataModelId != Guid.Empty)
            .WithMessage("请选择元数据生成模型.");
        validate.RuleFor(x => x.Workflow)
            .Must(x => x?.Embedding == null || x.Embedding.EmbedSourceText || x.Embedding.EmbedMetadata)
            .WithMessage("至少需要选择一种向量化内容（原文或元数据）。");
    }
}
