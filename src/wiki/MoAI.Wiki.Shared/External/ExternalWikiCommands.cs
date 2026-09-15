using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Models;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.External;

/// <summary>
/// 外部调用方身份（应用 token 解析后由 Controller 填充）.
/// </summary>
public class ExternalWikiCaller
{
    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 来源应用接入 id.
    /// </summary>
    public Guid AccessAppId { get; init; }
}

/// <summary>
/// 查询团队下的知识库列表（外部接口）.
/// </summary>
public class QueryExternalWikisCommand : IRequest<QueryWikisCommandResponse>, IModelValidator<QueryExternalWikisCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalWikisCommand> validate)
    {
        // Caller 由 Controller 从接入 token 解析填充，自动验证发生在回填之前，因此此处不校验。
    }
}

/// <summary>
/// 查询知识库详情（外部接口）.
/// </summary>
public class QueryExternalWikiCommand : IRequest<QueryWikiCommandResponse>, IModelValidator<QueryExternalWikiCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalWikiCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
    }
}

/// <summary>
/// 更新知识库向量化模型与维度配置（外部接口）.
/// 一旦该 wiki 已有文档被向量化（IsLock），模型与维度不可再修改（409）.
/// </summary>
public class UpdateExternalWikiEmbeddingCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateExternalWikiEmbeddingCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 向量化模型 id.
    /// </summary>
    public Guid EmbeddingModelId { get; init; }

    /// <summary>
    /// 知识库向量维度（1-2000）.
    /// </summary>
    public int EmbeddingDimensions { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateExternalWikiEmbeddingCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.EmbeddingModelId).NotEmpty().WithMessage("请选择向量化模型.");
        validate.RuleFor(x => x.EmbeddingDimensions)
            .GreaterThan(0).WithMessage("向量维度必须大于 0.")
            .LessThanOrEqualTo(2000).WithMessage("向量维度不能超过 2000（pgvector 索引硬上限）.");
    }
}

/// <summary>
/// 分页查询知识库文档列表（外部接口）.
/// </summary>
public class QueryExternalWikiDocumentsCommand : IRequest<QueryWikiDocumentsCommandResponse>, IModelValidator<QueryExternalWikiDocumentsCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 筛选文件名称.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// 是否已经向量化（null 表示不过滤）.
    /// </summary>
    public bool? IsEmbedding { get; init; }

    /// <summary>
    /// 包含的文件类型（如 .md、.docx）.
    /// </summary>
    public IReadOnlyCollection<string> IncludeFileTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 排除的文件类型（如 .md、.docx）.
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFileTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalWikiDocumentsCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.PageNo).GreaterThanOrEqualTo(1).WithMessage("页码不正确.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("每页条数不正确（1-200）.");
    }
}

/// <summary>
/// 预上传知识库文档，生成预签名上传地址（外部接口）.
/// </summary>
public class PreUploadExternalWikiDocumentCommand : IRequest<PreUploadWikiDocumentCommandResponse>, IModelValidator<PreUploadExternalWikiDocumentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 文件类型 (MIME Type).
    /// </summary>
    public string ContentType { get; init; } = default!;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string SHA256 { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadExternalWikiDocumentCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名称不能为空.").MaximumLength(255).WithMessage("文件名称最长 255 个字符.");
        validate.RuleFor(x => x.ContentType).NotEmpty().WithMessage("文件类型不能为空.");
        validate.RuleFor(x => x.FileSize).GreaterThan(0).WithMessage("文件大小必须大于 0.").LessThan(1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
        validate.RuleFor(x => x.SHA256).NotEmpty().WithMessage("文件 SHA256 不能为空.");
    }
}

/// <summary>
/// 完成知识库文档上传（外部接口）.
/// </summary>
public class CompleteExternalWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<CompleteExternalWikiDocumentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 文件 id.
    /// </summary>
    public long FileId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CompleteExternalWikiDocumentCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.FileId).GreaterThan(0).WithMessage("文件 id 不正确.");
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名称不能为空.").MaximumLength(255).WithMessage("文件名称最长 255 个字符.");
    }
}

/// <summary>
/// 删除知识库文档（外部接口）.
/// </summary>
public class DeleteExternalWikiDocumentsCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteExternalWikiDocumentsCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id 集合.
    /// </summary>
    public IReadOnlyCollection<long> DocumentIds { get; init; } = Array.Empty<long>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteExternalWikiDocumentsCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.DocumentIds).NotEmpty().WithMessage("文档 id 不正确.").Must(x => x.All(id => id > 0)).WithMessage("文档 id 不正确.");
    }
}

/// <summary>
/// 重命名知识库文档（外部接口）.
/// </summary>
public class RenameExternalWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<RenameExternalWikiDocumentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 新的文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RenameExternalWikiDocumentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不做校验。
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名称不能为空.").MaximumLength(255).WithMessage("文件名称最长 255 个字符.");
    }
}

/// <summary>
/// 读取知识库文档已提取的完整内容（markdown）（外部接口）.
/// </summary>
public class GetExternalWikiDocumentContentCommand : IRequest<SimpleString>, IModelValidator<GetExternalWikiDocumentContentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<GetExternalWikiDocumentContentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段（无）.
    }
}

/// <summary>
/// 提取知识库文档内容（外部接口）.
/// 文档上传后默认未提取内容，需先执行本命令生成内容，之后才能进行切割。
/// </summary>
public class ExtractExternalDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<ExtractExternalDocumentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExtractExternalDocumentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段（无）.
    }
}

/// <summary>
/// 普通切割知识库文档（外部接口）.
/// 需先执行 <see cref="ExtractExternalDocumentCommand"/> 提取内容，之后才能切割。
/// </summary>
public class PartitionExternalDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<PartitionExternalDocumentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 切割模式.
    /// </summary>
    public DocumentPartitionSplitMode SplitMode { get; init; } = DocumentPartitionSplitMode.Markdown;

    /// <summary>
    /// 切片大小（1-8192，单位由 <see cref="SizeUnit"/> 决定）.
    /// </summary>
    public int ChunkSize { get; init; }

    /// <summary>
    /// 切片重叠大小（0-8192，单位由 <see cref="OverlapUnit"/> 决定）.
    /// </summary>
    public int ChunkOverlap { get; init; }

    /// <summary>
    /// 重叠单位.
    /// </summary>
    public DocumentPartitionOverlapUnit OverlapUnit { get; init; } = DocumentPartitionOverlapUnit.Character;

    /// <summary>
    /// 切片大小计量单位.
    /// </summary>
    public DocumentPartitionSizeUnit SizeUnit { get; init; } = DocumentPartitionSizeUnit.Character;

    /// <summary>
    /// Token 计量时使用的编码名或模型名，为空默认 cl100k_base.
    /// </summary>
    public string? TokenEncodingOrModel { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PartitionExternalDocumentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.ChunkSize)
            .GreaterThan(0).WithMessage("切片大小必须大于 0.")
            .LessThanOrEqualTo(8192).WithMessage("切片大小不能超过 8192.");
        validate.RuleFor(x => x.ChunkOverlap)
            .GreaterThanOrEqualTo(0).WithMessage("切片重叠不能小于 0.")
            .LessThanOrEqualTo(8192).WithMessage("切片重叠不能超过 8192.");
        validate.RuleFor(x => x.ChunkOverlap)
            .LessThan(x => x.ChunkSize).WithMessage("按字符重叠时，切片重叠必须小于切片大小.")
            .When(x => x.OverlapUnit == DocumentPartitionOverlapUnit.Character);
        validate.RuleFor(x => x.TokenEncodingOrModel)
            .MaximumLength(64).WithMessage("Token 编码或模型名不能超过 64 个字符.");
    }
}

/// <summary>
/// AI 智能切割知识库文档（外部接口）.
/// 需先执行 <see cref="ExtractExternalDocumentCommand"/> 提取内容，之后才能切割。
/// </summary>
public class AiPartitionExternalDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<AiPartitionExternalDocumentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 用于智能切割的对话模型 id.
    /// </summary>
    public Guid AiModelId { get; init; }

    /// <summary>
    /// 提示词模板，为空时使用内置默认模板（要求模型输出 JSON 字符串数组）.
    /// </summary>
    public string? PromptTemplate { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AiPartitionExternalDocumentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("请选择用于智能切割的对话模型.");
    }
}

/// <summary>
/// 触发知识库文档向量化（外部接口）.
/// 需先提取内容、切割生成切片（元数据采用已保存结果按本次触发选择是否参与向量化）.
/// </summary>
public class EmbedExternalDocumentCommand : IRequest<EmbeddingDocumentCommandResponse>, IModelValidator<EmbedExternalDocumentCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 是否对原文切片内容向量化.
    /// </summary>
    public bool IsEmbedSourceText { get; init; } = true;

    /// <summary>
    /// 是否对生成的元数据（大纲/问题/关键词/摘要）向量化.
    /// </summary>
    public bool IsEmbedMetadata { get; init; } = true;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<EmbedExternalDocumentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处校验请求体字段.
        validate.RuleFor(x => x)
            .Must(x => x.IsEmbedSourceText || x.IsEmbedMetadata)
            .WithMessage("至少需要选择一种向量化内容（原文或元数据）。");
    }
}

/// <summary>
/// 查询知识库文档向量化详情（配置 + 文档状态 + 切片列表）（外部接口）.
/// </summary>
public class QueryExternalDocumentEmbeddingCommand : IRequest<QueryWikiDocumentEmbeddingCommandResponse>, IModelValidator<QueryExternalDocumentEmbeddingCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalWikiCaller Caller { get; init; } = default!;

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalDocumentEmbeddingCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验.
    }
}
