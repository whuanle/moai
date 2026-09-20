using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 知识库文档接口.
/// </summary>
[ApiController]
[Route("/wiki/{wikiId}/documents")]
public class WikiDocumentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiDocumentController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public WikiDocumentController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询知识库文档列表，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentsCommandResponse"/>.</returns>
    [HttpPost("list")]
    public Task<QueryWikiDocumentsCommandResponse> QueryDocuments(long wikiId, [FromBody] QueryWikiDocumentsCommand req, CancellationToken ct)
    {
        var cmd = new QueryWikiDocumentsCommand
        {
            WikiId = wikiId,
            PageNo = req.PageNo,
            PageSize = req.PageSize,
            Query = req.Query,
            IsEmbedding = req.IsEmbedding,
            IncludeFileTypes = req.IncludeFileTypes,
            ExcludeFileTypes = req.ExcludeFileTypes
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 预上传知识库文档，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">预上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PreUploadWikiDocumentCommandResponse"/>.</returns>
    [HttpPost("preupload")]
    public Task<PreUploadWikiDocumentCommandResponse> PreUpload(long wikiId, [FromBody] PreUploadWikiDocumentCommand req, CancellationToken ct)
    {
        var cmd = new PreUploadWikiDocumentCommand
        {
            WikiId = wikiId,
            FileName = req.FileName,
            ContentType = req.ContentType,
            FileSize = req.FileSize,
            SHA256 = req.SHA256
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 完成知识库文档上传，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">完成上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("complete")]
    public Task<EmptyCommandResponse> Complete(long wikiId, [FromBody] CompleteWikiDocumentCommand req, CancellationToken ct)
    {
        var cmd = new CompleteWikiDocumentCommand
        {
            WikiId = wikiId,
            IsSuccess = req.IsSuccess,
            FileId = req.FileId,
            FileName = req.FileName
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除知识库文档，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">删除请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete]
    public Task<EmptyCommandResponse> Delete(long wikiId, [FromBody] DeleteWikiDocumentsCommand req, CancellationToken ct)
    {
        var cmd = new DeleteWikiDocumentsCommand
        {
            WikiId = wikiId,
            DocumentIds = req.DocumentIds
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 下载知识库文档，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleString"/> 包含下载地址.</returns>
    [HttpGet("{documentId}/download")]
    public Task<SimpleString> Download(long wikiId, long documentId, CancellationToken ct)
    {
        return _mediator.Send(new DownloadWikiDocumentCommand { WikiId = wikiId, DocumentId = documentId }, ct);
    }

    /// <summary>
    /// 读取知识库文档已提取的完整内容（markdown），仅团队成员可访问；用于内容区「全部加载」.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleString"/> 包含完整 markdown 内容.</returns>
    [HttpGet("{documentId}/content")]
    public Task<SimpleString> GetContent(long wikiId, long documentId, CancellationToken ct)
    {
        return _mediator.Send(new GetWikiDocumentContentCommand { WikiId = wikiId, DocumentId = documentId }, ct);
    }

    /// <summary>
    /// 重命名知识库文档，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">重命名请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{documentId}/rename")]
    public Task<EmptyCommandResponse> Rename(long wikiId, long documentId, [FromBody] RenameWikiDocumentCommand req, CancellationToken ct)
    {
        var cmd = new RenameWikiDocumentCommand
        {
            WikiId = wikiId,
            DocumentId = documentId,
            FileName = req.FileName
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 提取知识库文档内容（Maomi.ToMarkdown 抽取 markdown 并入库），仅团队成员可访问；切割前必须先执行本操作.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{documentId}/extract")]
    public Task<EmptyCommandResponse> Extract(long wikiId, long documentId, CancellationToken ct)
    {
        return _mediator.Send(new ExtractDocumentContentCommand { WikiId = wikiId, DocumentId = documentId }, ct);
    }

    /// <summary>
    /// 普通切割知识库文档（按字符 + 重叠固定切分），仅团队成员可访问；需先提取内容.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">切割请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{documentId}/partition")]
    public Task<EmptyCommandResponse> Partition(long wikiId, long documentId, [FromBody] PartitionDocumentCommand req, CancellationToken ct)
    {
        var cmd = new PartitionDocumentCommand
        {
            WikiId = wikiId,
            DocumentId = documentId,
            SplitMode = req.SplitMode,
            ChunkSize = req.ChunkSize,
            ChunkOverlap = req.ChunkOverlap,
            OverlapUnit = req.OverlapUnit,
            SizeUnit = req.SizeUnit,
            TokenEncodingOrModel = req.TokenEncodingOrModel,
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// AI 智能切割知识库文档（对话模型按语义输出 JSON 字符串数组），仅团队成员可访问；需先提取内容.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">AI 切割请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{documentId}/ai-partition")]
    public Task<EmptyCommandResponse> AiPartition(long wikiId, long documentId, [FromBody] AiPartitionDocumentCommand req, CancellationToken ct)
    {
        var cmd = new AiPartitionDocumentCommand
        {
            WikiId = wikiId,
            DocumentId = documentId,
            AiModelId = req.AiModelId,
            PromptTemplate = req.PromptTemplate,
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 为单个知识库文档切片生成元数据，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="chunkId">切片 id.</param>
    /// <param name="req">元数据生成请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{documentId}/chunks/{chunkId}/metadata/generate")]
    public Task<SimpleInt> GenerateChunkMetadata(long wikiId, long documentId, long chunkId, [FromBody] GenerateDocumentChunkMetadataCommand req, CancellationToken ct)
    {
        var cmd = new GenerateDocumentChunkMetadataCommand
        {
            WikiId = wikiId,
            DocumentId = documentId,
            MetadataModelId = req.MetadataModelId,
            ChunkIds = [chunkId],
            AppendExisting = req.AppendExisting,
            StrategyType = req.StrategyType,
        };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 为知识库文档切片批量生成元数据，仅团队成员可访问；未传切片 id 时处理全部切片.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">元数据生成请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{documentId}/chunks/metadata/generate")]
    public Task<SimpleInt> GenerateChunksMetadata(long wikiId, long documentId, [FromBody] GenerateDocumentChunkMetadataCommand req, CancellationToken ct)
    {
        var cmd = new GenerateDocumentChunkMetadataCommand
        {
            WikiId = wikiId,
            DocumentId = documentId,
            MetadataModelId = req.MetadataModelId,
            ChunkIds = req.ChunkIds,
            AppendExisting = req.AppendExisting,
            StrategyType = req.StrategyType,
        };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 触发知识库文档向量化，仅团队成员可访问；复用已提取内容与已切割切片，可按请求选择原文与已保存元数据.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">向量化请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmbeddingDocumentCommandResponse"/>.</returns>
    [HttpPost("{documentId}/embedding")]
    public Task<EmbeddingDocumentCommandResponse> Embedding(long wikiId, long documentId, [FromBody] EmbeddingDocumentCommand req, CancellationToken ct)
    {
        var cmd = new EmbeddingDocumentCommand
        {
            WikiId = wikiId,
            DocumentId = documentId,
            IsEmbedSourceText = req.IsEmbedSourceText,
            IsEmbedMetadata = req.IsEmbedMetadata,
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 批量执行知识库文档工作流：按勾选步骤（切割 / 生成元数据 / 向量化）一次性处理多个文档，也可只执行其中一步，仅团队成员可访问.
    /// 切割同步执行并逐文档隔离失败；元数据生成与向量化为异步任务，逐文档返回任务 id.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">批量工作流请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="BatchRunWikiDocumentWorkflowCommandResponse"/>.</returns>
    [HttpPost("batch-workflow")]
    public Task<BatchRunWikiDocumentWorkflowCommandResponse> BatchRunWorkflow(long wikiId, [FromBody] BatchRunWikiDocumentWorkflowCommand req, CancellationToken ct)
    {
        var cmd = new BatchRunWikiDocumentWorkflowCommand
        {
            WikiId = wikiId,
            DocumentIds = req.DocumentIds,
            IsPartition = req.IsPartition,
            IsAiPartition = req.IsAiPartition,
            AiModelId = req.AiModelId,
            PromptTemplate = req.PromptTemplate,
            SplitMode = req.SplitMode,
            ChunkSize = req.ChunkSize,
            ChunkOverlap = req.ChunkOverlap,
            OverlapUnit = req.OverlapUnit,
            SizeUnit = req.SizeUnit,
            TokenEncodingOrModel = req.TokenEncodingOrModel,
            IsGenerateMetadata = req.IsGenerateMetadata,
            MetadataModelId = req.MetadataModelId,
            StrategyTypes = req.StrategyTypes,
            IsEmbedding = req.IsEmbedding,
            EmbedSourceText = req.EmbedSourceText,
            EmbedMetadata = req.EmbedMetadata,
        };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询知识库文档向量化详情，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentEmbeddingCommandResponse"/>.</returns>
    [HttpGet("{documentId}/embedding")]
    public Task<QueryWikiDocumentEmbeddingCommandResponse> QueryEmbedding(long wikiId, long documentId, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikiDocumentEmbeddingCommand { WikiId = wikiId, DocumentId = documentId }, ct);
    }
}
