namespace MoAI.Wiki.Models;

/// <summary>
/// 携带飞书文档源参数的命令，用于复用创建/更新外部源的飞书校验规则.
/// 仅声明两个命令共有的字段；深度/数量等因创建与更新语义不同（创建为值、更新为可选），
/// 由各自的 <c>Validate</c> 分别约束.
/// </summary>
public interface IWikiFeishuSourceCommand
{
    /// <summary>
    /// 外部源类型.
    /// </summary>
    WikiSourceType SourceType { get; }

    /// <summary>
    /// 飞书知识空间节点 token.
    /// </summary>
    string? NodeToken { get; }

    /// <summary>
    /// 方式一：选择团队已有的飞书应用连接 id.
    /// </summary>
    System.Guid? FeishuAppId { get; }

    /// <summary>
    /// 方式二：新建飞书应用连接的名称.
    /// </summary>
    string? NewAppName { get; }

    /// <summary>
    /// 方式二：飞书开放平台 AppID.
    /// </summary>
    string? NewAppId { get; }

    /// <summary>
    /// 方式二：飞书开放平台 AppSecret.
    /// </summary>
    string? NewAppSecret { get; }

    /// <summary>
    /// 方式二：接入域名.
    /// </summary>
    string? NewAppDomain { get; }
}
