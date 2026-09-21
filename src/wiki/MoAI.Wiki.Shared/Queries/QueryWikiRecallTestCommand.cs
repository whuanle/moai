using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 知识库召回测试：在单个知识库范围内检索切片，支持文档范围过滤、相似度阈值、AI 优化问题与 AI 生成回答，仅团队成员可访问.
/// </summary>
public class QueryWikiRecallTestCommand : IRequest<QueryWikiRecallTestCommandResponse>, IUserIdContext, IModelValidator<QueryWikiRecallTestCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 查询文本.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 文档范围过滤（文档 id 集合）；为空表示全部文档.
    /// </summary>
    public List<long> DocumentIds { get; init; } = new();

    /// <summary>
    /// 返回条数，1-50，默认 5.
    /// </summary>
    public int Top { get; init; } = 5;

    /// <summary>
    /// 相似度阈值（0-1，含），null 表示不过滤；相似度得分低于阈值的命中项将被丢弃.
    /// </summary>
    public double? MinScore { get; init; }

    /// <summary>
    /// AI 优化问题 / 生成回答使用的对话模型 id；开启 <see cref="IsOptimizeQuery"/> 或 <see cref="IsAnswer"/> 时必填.
    /// </summary>
    public Guid? AiModelId { get; init; }

    /// <summary>
    /// 是否先由 AI 将问题优化为适合检索的查询文本.
    /// </summary>
    public bool IsOptimizeQuery { get; init; }

    /// <summary>
    /// 是否基于召回内容生成 AI 回答.
    /// </summary>
    public bool IsAnswer { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiRecallTestCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .GreaterThan(0).WithMessage("知识库 id 不正确.");
        validate.RuleFor(x => x.Query)
            .NotEmpty().WithMessage("请输入查询文本.")
            .MaximumLength(1000).WithMessage("查询文本长度不能超过 1000.");
        validate.RuleForEach(x => x.DocumentIds)
            .GreaterThan(0).WithMessage("文档 id 无效.");
        validate.RuleFor(x => x.Top)
            .InclusiveBetween(1, 50).WithMessage("返回条数必须在 1-50 之间.");
        validate.RuleFor(x => x.MinScore)
            .InclusiveBetween(0d, 1d).WithMessage("相似度阈值必须在 0-1 之间.")
            .When(x => x.MinScore.HasValue);
        validate.RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("请选择 AI 对话模型.")
            .When(x => x.IsOptimizeQuery || x.IsAnswer);
    }
}
