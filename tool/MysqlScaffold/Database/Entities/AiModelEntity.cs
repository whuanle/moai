using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// ai模型.
/// </summary>
[Table("ai_model")]
[Index("AiModelType", Name = "ai_model_ai_model_type_index")]
[Index("AiProvider", Name = "ai_model_ai_provider_index")]
public partial class AiModelEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 自定义名模型名称，便于用户选择.
    /// </summary>
    [Column("title")]
    [StringLength(50)]
    public string Title { get; set; } = default!;

    /// <summary>
    /// 模型功能类型.
    /// </summary>
    [Column("ai_model_type")]
    [StringLength(20)]
    public string AiModelType { get; set; } = default!;

    /// <summary>
    /// 模型供应商.
    /// </summary>
    [Column("ai_provider")]
    [StringLength(50)]
    public string AiProvider { get; set; } = default!;

    /// <summary>
    /// 模型名称,gpt-4o.
    /// </summary>
    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// 部署名称,Azure需要.
    /// </summary>
    [Column("deployment_name")]
    [StringLength(100)]
    public string DeploymentName { get; set; } = default!;

    /// <summary>
    /// 端点.
    /// </summary>
    [Column("endpoint")]
    [StringLength(100)]
    public string Endpoint { get; set; } = default!;

    /// <summary>
    /// 密钥.
    /// </summary>
    [Column("key")]
    [StringLength(100)]
    public string Key { get; set; } = default!;

    /// <summary>
    /// 支持函数.
    /// </summary>
    [Column("function_call")]
    public bool FunctionCall { get; set; }

    /// <summary>
    /// 上下文最大token数量.
    /// </summary>
    [Column("context_window_tokens", TypeName = "int(11)")]
    public int ContextWindowTokens { get; set; }

    /// <summary>
    /// 最大文本输出token.
    /// </summary>
    [Column("text_output", TypeName = "int(11)")]
    public int TextOutput { get; set; }

    /// <summary>
    /// 向量的维度.
    /// </summary>
    [Column("max_dimension", TypeName = "int(11)")]
    public int MaxDimension { get; set; }

    /// <summary>
    /// 支持文件上传.
    /// </summary>
    [Column("files")]
    public bool Files { get; set; }

    /// <summary>
    /// 支持图片输出.
    /// </summary>
    [Column("image_output")]
    public bool ImageOutput { get; set; }

    /// <summary>
    /// 支持计算机视觉.
    /// </summary>
    [Column("is_vision")]
    public bool IsVision { get; set; }

    /// <summary>
    /// 是否开放给大家使用.
    /// </summary>
    [Column("is_public")]
    public bool IsPublic { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    [Column("create_user_id", TypeName = "int(11)")]
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    [Column("create_time", TypeName = "datetime")]
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    [Column("update_user_id", TypeName = "int(11)")]
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    [Column("update_time", TypeName = "datetime")]
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    [Column("is_deleted", TypeName = "bigint(20)")]
    public long IsDeleted { get; set; }
}
