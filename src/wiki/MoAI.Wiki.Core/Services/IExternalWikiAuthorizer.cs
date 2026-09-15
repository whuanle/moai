using MoAI.Database.Entities;

namespace MoAI.Wiki.Services;

/// <summary>
/// 外部应用 token 的知识库授权器：应用 token 携带 TeamId，对团队知识库权限等价团队 Admin.
/// </summary>
public interface IExternalWikiAuthorizer
{
    /// <summary>
    /// 校验知识库属于外部调用方团队；不存在或跨团队一律 404（对外不泄露存在性）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="teamId">外部调用方团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回校验通过的知识库.</returns>
    Task<WikiEntity> AuthorizeAsync(long wikiId, long teamId, CancellationToken cancellationToken);
}
