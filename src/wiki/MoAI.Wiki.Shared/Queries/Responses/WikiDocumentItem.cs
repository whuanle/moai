using MoAI.Infra.Models;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档项.
/// </summary>
public class WikiDocumentItem : AuditsInfo
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文件 id.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// 是否已经向量化.
    /// </summary>
    public bool IsEmbedding { get; set; }

    /// <summary>
    /// 是否已提取内容（上传后自动提取入库，wiki_document_content 有内容）.
    /// </summary>
    public bool IsContentExtracted { get; set; }

    /// <summary>
    /// 已提取内容长度（字节/字符）.
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// 切片数量.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// 元数据数量.
    /// </summary>
    public int MetadataCount { get; set; }
}
