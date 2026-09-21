using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询外部源已同步的文档列表，仅团队成员可访问.
/// </summary>
public class QueryWikiSourceDocumentsCommand : PagedParamter, IRequest<QueryWikiSourceDocumentsCommandResponse>, IModelValidator<QueryWikiSourceDocumentsCommand>, IUserIdContext
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 外部源 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid SourceId { get; init; }

    /// <summary>
    /// 标题关键字筛选.
    /// </summary>
    public string? Query { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiSourceDocumentsCommand> validate)
    {
        // WikiId/SourceId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.Query)
            .MaximumLength(255).WithMessage("搜索关键字不能超过 255 个字符.");
    }
}

/// <summary>
/// 外部源文档列表响应.
/// </summary>
public class QueryWikiSourceDocumentsCommandResponse
{
    /// <summary>
    /// 符合条件的总数.
    /// </summary>
    public long Total { get; init; }

    /// <summary>
    /// 当前页文档映射列表.
    /// </summary>
    public List<WikiSourceDocumentItem> Items { get; init; } = new();
}
