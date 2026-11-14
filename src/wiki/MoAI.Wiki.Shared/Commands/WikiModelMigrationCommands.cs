using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 发起知识库向量化模型迁移：以新模型/维度重建该库全部向量（临时集合 + 事务换名），完成后更新知识库配置.
/// 仅团队管理员可操作；同一知识库同时只允许一个活跃迁移任务，迁移期间该库提取/切割/向量化被阻断.
/// </summary>
public class StartWikiModelMigrationCommand : IRequest<StartWikiModelMigrationCommandResponse>, IModelValidator<StartWikiModelMigrationCommand>, IUserIdContext
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 目标向量化模型 id（团队可用的 embedding 模型）.
    /// </summary>
    public Guid ModelId { get; init; }

    /// <summary>
    /// 目标向量维度（1-2000）.
    /// </summary>
    public int Dimensions { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<StartWikiModelMigrationCommand> validate)
    {
        validate.RuleFor(x => x.ModelId).NotEmpty().WithMessage("请选择目标向量化模型.");
        validate.RuleFor(x => x.Dimensions).InclusiveBetween(1, 2000).WithMessage("向量维度须在 1-2000 之间.");
    }
}

/// <summary>
/// 发起迁移响应.
/// </summary>
public class StartWikiModelMigrationCommandResponse
{
    /// <summary>
    /// 迁移任务 id.
    /// </summary>
    public Guid TaskId { get; init; }
}

/// <summary>
/// 查询知识库向量化模型迁移状态（当前配置 + 向量总数 + 最近任务进度）.
/// </summary>
public class QueryWikiModelMigrationStatusCommand : IRequest<QueryWikiModelMigrationStatusResponse>, IUserIdContext
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
}

/// <summary>
/// 迁移状态响应.
/// </summary>
public class QueryWikiModelMigrationStatusResponse
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 当前生效的向量化模型 id.
    /// </summary>
    public Guid CurrentModelId { get; init; }

    /// <summary>
    /// 当前生效的向量化模型名称.
    /// </summary>
    public string CurrentModelName { get; init; } = string.Empty;

    /// <summary>
    /// 当前向量维度.
    /// </summary>
    public int CurrentDimensions { get; init; }

    /// <summary>
    /// 该库当前向量总条数.
    /// </summary>
    public int TotalVectorCount { get; init; }

    /// <summary>
    /// 最近一次迁移任务（活跃任务优先，含进度）；从未迁移时为 null.
    /// </summary>
    public WikiModelMigrationTaskState? ActiveMigration { get; init; }
}

/// <summary>
/// 迁移任务状态（含进度回显）.
/// </summary>
public class WikiModelMigrationTaskState
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }

    /// <summary>
    /// 任务状态：1 等待 / 2 处理中 / 4 成功 / 5 失败.
    /// </summary>
    public int State { get; init; }

    /// <summary>
    /// 任务消息（进度或失败原因）.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 目标向量化模型 id.
    /// </summary>
    public Guid TargetModelId { get; init; }

    /// <summary>
    /// 目标向量化模型名称.
    /// </summary>
    public string TargetModelName { get; init; } = string.Empty;

    /// <summary>
    /// 目标向量维度.
    /// </summary>
    public int TargetDimensions { get; init; }

    /// <summary>
    /// 计划重建的向量总条数.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 已重建的向量条数.
    /// </summary>
    public int MigratedCount { get; init; }

    /// <summary>
    /// 任务创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 任务最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }
}
