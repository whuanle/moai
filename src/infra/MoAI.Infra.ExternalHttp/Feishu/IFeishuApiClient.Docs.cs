#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using MoAI.Infra.Feishu.Models;
using Refit;

namespace MoAI.Infra.Feishu;

/// <summary>
/// 飞书接口.
/// </summary>
public partial interface IFeishuApiClient
{
    /// <summary>
    /// 获取知识空间列表.
    /// </summary>
    /// <remarks>
    /// 此接口用于获取有权限访问的知识空间列表。
    /// 使用 tenant access token 调用时，请确认应用或机器人拥有部分知识空间的访问权限，否则返回列表为空。
    /// 此接口为分页接口。由于权限过滤，可能返回列表为空，但当分页标记（has_more）为 true 时，可以继续分页请求。
    /// 此接口不会返回我的文档库。
    /// 最后更新于 2025-08-22.
    /// </remarks>
    /// <param name="authorization"></param>
    /// <param name="request">请求参数.</param>
    /// <returns>知识空间列表.</returns>
    [Get("/open-apis/wiki/v2/spaces")]
    Task<GetWikiSpacesResponse> GetWikiSpacesAsync([Header("Authorization")] string authorization, [Query] GetWikiSpacesRequest request);

    /// <summary>
    /// 获取知识空间信息.
    /// </summary>
    /// <remarks>
    /// 此接口用于根据知识空间 ID 查询知识空间的信息，包括空间的类型、可见性、分享状态等。
    /// 最后更新于 2025-07-22.
    /// </remarks>
    /// <param name="authorization"></param>
    /// <param name="spaceId">知识空间 ID.</param>
    /// <param name="lang">指定返回的文档库名称展示语言，当查询我的文档库时生效。</param>
    /// <returns>知识空间信息.</returns>
    [Get("/open-apis/wiki/v2/spaces/{space_id}")]
    Task<GetWikiSpaceInfoResponse> GetWikiSpaceInfoAsync([Header("Authorization")] string authorization, [AliasAs("space_id")] string spaceId, [Query] string? lang = null);

    /// <summary>
    /// 获取知识空间子节点列表.
    /// </summary>
    /// <remarks>
    /// <para>此接口用于分页获取Wiki节点的子节点列表。</para>
    /// <para>此接口为分页接口。由于权限过滤，可能返回列表为空，但分页标记（has_more）为true，可以继续分页请求。</para>
    /// <para>知识库权限要求，当前使用的 access token 所代表的应用或用户拥有：父节点阅读权限。</para>
    /// <para>最后更新于 2025-08-19.</para>
    /// </remarks>
    /// <param name="authorization"></param>
    /// <param name="spaceId">知识空间id，如果查询我的文档库可替换为my_library。</param>
    /// <param name="request">请求参数。</param>
    /// <returns>知识空间子节点列表。</returns>
    [Get("/open-apis/wiki/v2/spaces/{space_id}/nodes")]
    Task<GetWikiNodesResponse> GetWikiNodesAsync([Header("Authorization")] string authorization, [AliasAs("space_id")] string spaceId, [Query] GetWikiNodesRequest request);

    /// <summary>
    /// 获取文档基本信息.
    /// </summary>
    /// <remarks>
    /// <para>获取文档标题和最新版本 ID。</para>
    /// <para>最后更新于 2025-01-03.</para>
    /// </remarks>
    /// <param name="authorization"></param>
    /// <param name="documentId">文档的唯一标识。</param>
    /// <returns>文档基本信息。</returns>
    [Get("/open-apis/docx/v1/documents/{document_id}")]
    Task<GetDocumentInfoResponse> GetDocumentInfoAsync([Header("Authorization")] string authorization, [AliasAs("document_id")] string documentId);

    /// <summary>
    /// 获取文档纯文本内容。
    /// </summary>
    /// <remarks>
    /// <para>获取文档的纯文本内容。</para>
    /// <para>应用频率限制：单个应用调用频率上限为每秒 5 次。</para>
    /// <para>调用此接口前，请确保当前调用身份已有云文档的阅读权限。</para>
    /// <para>最后更新于 2025-01-03。</para>
    /// </remarks>
    /// <param name="authorization"></param>
    /// <param name="documentId">文档的唯一标识。</param>
    /// <param name="lang">指定返回的 MentionUser 即 @用户 的语言。0：默认名称；1：英文名称。</param>
    /// <returns>文档纯文本内容。</returns>
    [Get("/open-apis/docx/v1/documents/{document_id}/raw_content")]
    Task<GetDocumentRawContentResponse> GetDocumentRawContentAsync([Header("Authorization")] string authorization, [AliasAs("document_id")] string documentId, [Query] int? lang = null);

    /// <summary>
    /// 获取文档所有块。
    /// </summary>
    /// <remarks>
    /// <para>获取文档所有块的富文本内容并分页返回。</para>
    /// <para>应用频率限制：单个应用调用频率上限为每秒 5 次。</para>
    /// </remarks>
    /// <param name="authorization">认证信息。</param>
    /// <param name="documentId">文档的唯一标识。</param>
    /// <param name="request">请求参数。</param>
    /// <returns>文档所有块。</returns>
    [Get("/open-apis/docx/v1/documents/{document_id}/blocks")]
    Task<GetDocumentBlocksResponse> GetDocumentBlocksAsync([Header("Authorization")] string authorization, [AliasAs("document_id")] string documentId, [Query] GetDocumentBlocksRequest request);
}