namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 知识库查询类型.
/// </summary>
public enum WikiQueryType
{
    /// <summary>
    /// 公开知识库.
    /// </summary>
    System,

    /// <summary>
    /// 自己创建的知识库.
    /// </summary>
    Own,

    /// <summary>
    /// 已加入的知识库.
    /// </summary>
    User
}