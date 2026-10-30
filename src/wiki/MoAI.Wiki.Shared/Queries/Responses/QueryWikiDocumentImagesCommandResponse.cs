namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档内嵌图片附件列表响应.
/// </summary>
public class QueryWikiDocumentImagesCommandResponse
{
    /// <summary>
    /// 图片附件集合，按文档内序号升序.
    /// </summary>
    public IReadOnlyList<WikiDocumentImageItem> Items { get; set; } = new List<WikiDocumentImageItem>();
}
