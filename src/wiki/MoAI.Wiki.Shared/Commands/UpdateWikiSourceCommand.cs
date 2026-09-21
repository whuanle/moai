using System;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 更新知识库外部源，需要团队 Admin 及以上角色.
/// 未传的字段保持原值；工作流配置整体覆盖（传 null 表示回退知识库默认工作流）.
/// </summary>
public class UpdateWikiSourceCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiSourceCommand>, IUserIdContext, IWikiSourceWorkflowCommand, IWikiCrawlerSourceCommand, IWikiFeishuSourceCommand
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 外部源 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid SourceId { get; init; }

    /// <summary>
    /// 外部源类型，仅为满足 <see cref="IWikiCrawlerSourceCommand"/> 契约而存在.
    /// 更新时外部源类型不可变更（以库里现状为准），故本命令的校验只按「本次提交了哪些字段」判定，不读该值.
    /// </summary>
    [JsonIgnore]
    public WikiSourceType SourceType { get; init; }

    /// <summary>
    /// 爬虫源配置，为空表示不修改.
    /// </summary>
    public WikiSourceCrawlerConfig? Crawler { get; init; }

    /// <summary>
    /// 外部源名称，为空表示不修改.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 描述，为空表示不修改.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 方式一：改绑到团队已有的飞书应用连接 id；为空表示不修改绑定.
    /// </summary>
    public Guid? FeishuAppId { get; init; }

    /// <summary>
    /// 方式二：新建飞书应用连接的名称.
    /// </summary>
    public string? NewAppName { get; init; }

    /// <summary>
    /// 方式二：飞书开放平台 AppID.
    /// </summary>
    public string? NewAppId { get; init; }

    /// <summary>
    /// 方式二：飞书开放平台 AppSecret.
    /// </summary>
    public string? NewAppSecret { get; init; }

    /// <summary>
    /// 方式二：接入域名.
    /// </summary>
    public string? NewAppDomain { get; init; }

    /// <summary>
    /// 飞书知识空间节点 token，为空表示不修改.
    /// </summary>
    public string? NodeToken { get; init; }

    /// <summary>
    /// 是否拉取节点下的全部子文档，为空表示不修改.
    /// </summary>
    public bool? IncludeSubNodes { get; init; }

    /// <summary>
    /// 子节点遍历最大深度，为空表示不修改.
    /// </summary>
    public int? MaxDepth { get; init; }

    /// <summary>
    /// 单次同步文档数量上限，为空表示不修改.
    /// </summary>
    public int? MaxDocuments { get; init; }

    /// <inheritdoc/>
    public WikiWorkflowConfig? Workflow { get; init; }

    /// <summary>
    /// 定时同步 cron 表达式（UTC），为空字符串表示关闭定时同步，为 null 表示不修改.
    /// </summary>
    public string? Cron { get; init; }

    /// <summary>
    /// 是否开启飞书事件订阅，为空表示不修改.
    /// </summary>
    public bool? IsEventSubscription { get; init; }

    /// <summary>
    /// 是否启用该外部源，为空表示不修改.
    /// </summary>
    public bool? IsEnable { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiSourceCommand> validate)
    {
        // WikiId/SourceId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("请输入外部源名称.")
            .MaximumLength(50).WithMessage("外部源名称不能超过 50 个字符.")
            .When(x => x.Name != null);
        validate.RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("描述不能超过 255 个字符.");
        validate.RuleFor(x => x.Cron)
            .Must(x => x == null || x.Length == 0 || WikiSourceDefaults.IsValidCron(x))
            .WithMessage("定时同步 cron 表达式不正确.");

        ValidateCrawler(validate);
        ValidateFeishu(validate);

        CreateWikiSourceCommand.ValidateWorkflow(validate);
    }

    /// <summary>
    /// 爬虫源配置校验（仅当本次提交携带爬虫配置时生效）.
    /// </summary>
    /// <typeparam name="T">命令类型.</typeparam>
    /// <param name="validate">校验器.</param>
    private static void ValidateCrawler<T>(AbstractValidator<T> validate)
        where T : IWikiCrawlerSourceCommand
    {
        validate.RuleFor(x => x.Crawler!.StartUrl)
            .Must(x => WikiSourceDefaults.IsValidCrawlerUrl(x))
            .WithMessage($"请输入合法的起始 URL（http/https，不超过 {WikiSourceDefaults.CrawlerUrlMaxLength} 个字符）.")
            .When(x => x.Crawler != null);
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
        // 与创建口径一致：只在本次提交了爬虫配置时校验其文本字段，未提交时跳过避免空引用
        validate.RuleFor(x => x.Crawler!.UserAgent)
            .MaximumLength(255).WithMessage("UserAgent 不能超过 255 个字符.")
            .When(x => x.Crawler != null);
        validate.RuleFor(x => x.Crawler!.ContentSelector)
            .MaximumLength(WikiSourceDefaults.CrawlerSelectorMaxLength)
            .WithMessage($"正文选择器不能超过 {WikiSourceDefaults.CrawlerSelectorMaxLength} 个字符.")
            .When(x => x.Crawler != null);
    }

    /// <summary>
    /// 飞书文档源参数校验（仅当本次提交涉及飞书参数时生效）.
    /// </summary>
    /// <typeparam name="T">命令类型.</typeparam>
    /// <param name="validate">校验器.</param>
    internal static void ValidateFeishu<T>(AbstractValidator<T> validate)
        where T : IWikiFeishuSourceCommand
    {
        validate.RuleFor(x => x.NodeToken)
            .NotEmpty().WithMessage("请输入飞书文档节点 token.")
            .MaximumLength(128).WithMessage("节点 token 不能超过 128 个字符.")
            .When(x => x.NodeToken != null);

        // 两种绑定方式互斥
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
        validate.RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.NewAppId) || !string.IsNullOrWhiteSpace(x.NewAppSecret))
            .WithMessage("新建连接需同时填写 AppID 与 AppSecret.");
    }
}
