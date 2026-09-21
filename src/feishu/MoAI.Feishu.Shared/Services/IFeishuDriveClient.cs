using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Feishu.Models;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书云文档客户端：知识空间节点遍历、文档原始内容读取与云文档事件订阅，供知识库外部源等模块使用.
/// </summary>
public interface IFeishuDriveClient
{
    /// <summary>
    /// 获取知识空间节点信息（按节点 token 反查空间 id、文档 token 与标题）.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id（feishu_app.id）.</param>
    /// <param name="nodeToken">知识空间节点 token.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>节点信息，不存在或无权限时为 null.</returns>
    Task<FeishuWikiNode?> GetWikiNodeAsync(Guid feishuAppId, string nodeToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定父节点下的子节点列表（自动翻页，最多 500 个）.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id（feishu_app.id）.</param>
    /// <param name="spaceId">知识空间 id.</param>
    /// <param name="parentNodeToken">父节点 token，为空表示取根节点下的子节点.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>子节点列表.</returns>
    Task<IReadOnlyList<FeishuWikiNode>> ListChildNodesAsync(Guid feishuAppId, string spaceId, string? parentNodeToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取新版文档（docx）的原始文本内容.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id（feishu_app.id）.</param>
    /// <param name="documentId">文档 token（obj_token）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文档原始内容.</returns>
    Task<string> GetDocxRawContentAsync(Guid feishuAppId, string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅云文档事件，订阅后文档变更才会推送事件（文档需已授权给该应用）.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id（feishu_app.id）.</param>
    /// <param name="fileToken">云文档 token.</param>
    /// <param name="fileType">云文档类型，默认 docx.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task SubscribeDocEventAsync(Guid feishuAppId, string fileToken, string fileType = "docx", CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消订阅云文档事件.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id（feishu_app.id）.</param>
    /// <param name="fileToken">云文档 token.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task UnsubscribeDocEventAsync(Guid feishuAppId, string fileToken, CancellationToken cancellationToken = default);
}
