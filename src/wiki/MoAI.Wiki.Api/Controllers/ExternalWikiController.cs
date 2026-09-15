using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Models;
using MoAI.App.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Commands;
using MoAI.Wiki.External;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 知识库外部开放接口：应用 token（团队级授权）管理知识库文档（上传/提取/切割/向量化）；认证由 ExternalAuthenticationMiddleware 统一处理.
/// </summary>
[ApiController]
[Route("/external/wiki")]
[AllowAnonymous]
public class ExternalWikiController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalWikiController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public ExternalWikiController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询 token 归属团队下的知识库列表（外部语义：团队级授权）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikisCommandResponse"/>.</returns>
    [HttpPost("list")]
    public async Task<QueryWikisCommandResponse> List(CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalWikisCommand { Caller = caller }, ct);
    }

    /// <summary>
    /// 查询知识库详情，含向量化模型配置（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiCommandResponse"/>.</returns>
    [HttpGet("{wikiId:long}")]
    public async Task<QueryWikiCommandResponse> Detail(long wikiId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalWikiCommand { Caller = caller, WikiId = wikiId }, ct);
    }

    /// <summary>
    /// 配置知识库向量化模型与维度（1-2000）；已有文档向量化后配置锁定，返回 409（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{wikiId:long}/embedding-config")]
    public async Task<EmptyCommandResponse> UpdateEmbeddingConfig(long wikiId, [FromBody] UpdateExternalWikiEmbeddingCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new UpdateExternalWikiEmbeddingCommand { Caller = caller, WikiId = wikiId, EmbeddingModelId = req.EmbeddingModelId, EmbeddingDimensions = req.EmbeddingDimensions }, ct);
    }

    /// <summary>
    /// 知识库文档分页列表，支持名称/向量化状态/文件类型过滤（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentsCommandResponse"/>.</returns>
    [HttpPost("{wikiId:long}/documents/list")]
    public async Task<QueryWikiDocumentsCommandResponse> ListDocuments(long wikiId, [FromBody] QueryExternalWikiDocumentsCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalWikiDocumentsCommand { Caller = caller, WikiId = wikiId, Query = req.Query, IsEmbedding = req.IsEmbedding, IncludeFileTypes = req.IncludeFileTypes, ExcludeFileTypes = req.ExcludeFileTypes, PageNo = req.PageNo, PageSize = req.PageSize }, ct);
    }

    /// <summary>
    /// 预上传文档，返回预签名上传地址，客户端直传存储（不经平台转发文件流）（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">预上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadWikiDocumentCommandResponse"/>.</returns>
    [HttpPost("{wikiId:long}/documents/preupload")]
    public async Task<PreUploadWikiDocumentCommandResponse> PreUploadDocument(long wikiId, [FromBody] PreUploadExternalWikiDocumentCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new PreUploadExternalWikiDocumentCommand { Caller = caller, WikiId = wikiId, FileName = req.FileName, ContentType = req.ContentType, FileSize = req.FileSize, SHA256 = req.SHA256 }, ct);
    }

    /// <summary>
    /// 完成文档上传落库；成功后自动提取内容（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">完成上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("{wikiId:long}/documents/complete")]
    public async Task<EmptyCommandResponse> CompleteDocument(long wikiId, [FromBody] CompleteExternalWikiDocumentCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new CompleteExternalWikiDocumentCommand { Caller = caller, WikiId = wikiId, IsSuccess = req.IsSuccess, FileId = req.FileId, FileName = req.FileName }, ct);
    }

    /// <summary>
    /// 批量删除文档（连带切片/元数据/存储文件）（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">删除请求（body 传 DocumentIds）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{wikiId:long}/documents")]
    public async Task<EmptyCommandResponse> DeleteDocuments(long wikiId, [FromBody] DeleteExternalWikiDocumentsCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new DeleteExternalWikiDocumentsCommand { Caller = caller, WikiId = wikiId, DocumentIds = req.DocumentIds }, ct);
    }

    /// <summary>
    /// 读取文档已提取的完整内容（markdown）（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleString"/> 包含完整 markdown 内容.</returns>
    [HttpGet("{wikiId:long}/documents/{documentId:long}/content")]
    public async Task<SimpleString> GetDocumentContent(long wikiId, long documentId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new GetExternalWikiDocumentContentCommand { Caller = caller, WikiId = wikiId, DocumentId = documentId }, ct);
    }

    /// <summary>
    /// 重命名文档（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">重命名请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{wikiId:long}/documents/{documentId:long}/rename")]
    public async Task<EmptyCommandResponse> RenameDocument(long wikiId, long documentId, [FromBody] RenameExternalWikiDocumentCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new RenameExternalWikiDocumentCommand { Caller = caller, WikiId = wikiId, DocumentId = documentId, FileName = req.FileName }, ct);
    }

    /// <summary>
    /// 触发文档内容提取（Maomi.ToMarkdown 抽取 markdown 并入库）；切割前必须先提取（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("{wikiId:long}/documents/{documentId:long}/extract")]
    public async Task<EmptyCommandResponse> ExtractDocument(long wikiId, long documentId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new ExtractExternalDocumentCommand { Caller = caller, WikiId = wikiId, DocumentId = documentId }, ct);
    }

    /// <summary>
    /// 普通切割文档（多模式切分 + 重叠）；需先提取内容（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">切割请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("{wikiId:long}/documents/{documentId:long}/partition")]
    public async Task<EmptyCommandResponse> PartitionDocument(long wikiId, long documentId, [FromBody] PartitionExternalDocumentCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new PartitionExternalDocumentCommand { Caller = caller, WikiId = wikiId, DocumentId = documentId, SplitMode = req.SplitMode, ChunkSize = req.ChunkSize, ChunkOverlap = req.ChunkOverlap, OverlapUnit = req.OverlapUnit, SizeUnit = req.SizeUnit, TokenEncodingOrModel = req.TokenEncodingOrModel }, ct);
    }

    /// <summary>
    /// AI 智能切割文档（对话模型按语义输出 JSON 字符串数组）；需先提取内容（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">AI 切割请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("{wikiId:long}/documents/{documentId:long}/ai-partition")]
    public async Task<EmptyCommandResponse> AiPartitionDocument(long wikiId, long documentId, [FromBody] AiPartitionExternalDocumentCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new AiPartitionExternalDocumentCommand { Caller = caller, WikiId = wikiId, DocumentId = documentId, AiModelId = req.AiModelId, PromptTemplate = req.PromptTemplate }, ct);
    }

    /// <summary>
    /// 触发文档向量化（异步队列），返回任务 id 供轮询；复用已提取内容与已切割切片（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">向量化请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmbeddingDocumentCommandResponse"/>.</returns>
    [HttpPost("{wikiId:long}/documents/{documentId:long}/embedding")]
    public async Task<EmbeddingDocumentCommandResponse> EmbedDocument(long wikiId, long documentId, [FromBody] EmbedExternalDocumentCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new EmbedExternalDocumentCommand { Caller = caller, WikiId = wikiId, DocumentId = documentId, IsEmbedSourceText = req.IsEmbedSourceText, IsEmbedMetadata = req.IsEmbedMetadata }, ct);
    }

    /// <summary>
    /// 查询文档向量化详情（配置 + 状态 + 切片列表 + embeddingCount），供轮询进度（外部语义：团队级授权）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentEmbeddingCommandResponse"/>.</returns>
    [HttpGet("{wikiId:long}/documents/{documentId:long}/embedding")]
    public async Task<QueryWikiDocumentEmbeddingCommandResponse> QueryDocumentEmbedding(long wikiId, long documentId, CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QueryExternalDocumentEmbeddingCommand { Caller = caller, WikiId = wikiId, DocumentId = documentId }, ct);
    }

    private ExternalWikiCaller RequireCaller()
    {
        // 从 HttpContext.Items 取认证中间件写入的 token 上下文（ExternalJwtBearerAuthenticationHandler 填充）
        var tokenContext = HttpContext.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var value) ? value as ExternalTokenContext : null;
        if (tokenContext == null)
        {
            throw new BusinessException("外部 token 无效.") { StatusCode = 401 };
        }

        if (tokenContext.SubjectType != UserType.ExternalApp)
        {
            throw new BusinessException("该接口仅支持应用 token.") { StatusCode = 403 };
        }

        if (tokenContext.AccessAppId == null)
        {
            throw new BusinessException("外部 token 缺少应用接入标识.") { StatusCode = 401 };
        }

        return new ExternalWikiCaller { TeamId = tokenContext.TeamId, AccessAppId = tokenContext.AccessAppId.Value };
    }
}
