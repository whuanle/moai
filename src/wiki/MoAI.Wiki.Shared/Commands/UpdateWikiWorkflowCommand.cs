using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 更新知识库默认工作流配置（切割 / 元数据生成 / 向量化三步预设），需要团队 Admin 及以上角色.
/// 整体覆盖保存：某步骤传 null 表示清除该步骤预设；仅保存预设，不触发文档处理.
/// 该预设作为批量处理与外部源同步在工作流缺省时的回退值.
/// </summary>
public class UpdateWikiWorkflowCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiWorkflowCommand>, IUserIdContext, IWikiSourceWorkflowCommand
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档切割预设，为空表示清除该步骤预设.
    /// </summary>
    public WikiWorkflowPartitionOptions? Partition { get; init; }

    /// <summary>
    /// 元数据生成预设，为空表示清除该步骤预设.
    /// </summary>
    public WikiWorkflowMetadataOptions? Metadata { get; init; }

    /// <summary>
    /// 向量化预设，为空表示清除该步骤预设.
    /// </summary>
    public WikiWorkflowEmbeddingOptions? Embedding { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public WikiWorkflowConfig? Workflow => new()
    {
        Partition = Partition,
        Metadata = Metadata,
        Embedding = Embedding,
    };

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiWorkflowCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        CreateWikiSourceCommand.ValidateWorkflow(validate);
    }
}
