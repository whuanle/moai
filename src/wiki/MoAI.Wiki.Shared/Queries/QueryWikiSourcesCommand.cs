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
/// 查询知识库外部源列表，仅团队成员可访问.
/// </summary>
public class QueryWikiSourcesCommand : IRequest<QueryWikiSourcesCommandResponse>, IModelValidator<QueryWikiSourcesCommand>, IUserIdContext
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiSourcesCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
    }
}

/// <summary>
/// 知识库外部源列表响应.
/// </summary>
public class QueryWikiSourcesCommandResponse
{
    /// <summary>
    /// 当前用户在知识库所属团队的角色（0=Member 1=Admin 2=Owner）.
    /// </summary>
    public int MyRole { get; init; }

    /// <summary>
    /// 外部源列表.
    /// </summary>
    public List<WikiSourceItem> Items { get; init; } = new();
}
