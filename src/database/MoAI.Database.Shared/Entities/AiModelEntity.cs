using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// ai模型.
/// </summary>
public partial class AiModelEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 自定义名模型名称，便于用户选择.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 模型功能类型.
    /// </summary>
    public string AiModelType { get; set; } = default!;

    /// <summary>
    /// 模型供应商.
    /// </summary>
    public string AiProvider { get; set; } = default!;

    /// <summary>
    /// 模型名称,gpt-4o.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 部署名称,Azure需要.
    /// </summary>
    public string DeploymentName { get; set; } = default!;

    /// <summary>
    /// 端点.
    /// </summary>
    public string Endpoint { get; set; } = default!;

    /// <summary>
    /// 密钥.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 支持函数.
    /// </summary>
    public bool FunctionCall { get; set; }

    /// <summary>
    /// 上下文最大token数量.
    /// </summary>
    public int ContextWindowTokens { get; set; }

    /// <summary>
    /// 最大文本输出token.
    /// </summary>
    public int TextOutput { get; set; }

    /// <summary>
    /// 向量的维度.
    /// </summary>
    public int MaxDimension { get; set; }

    /// <summary>
    /// 支持文件上传.
    /// </summary>
    public bool Files { get; set; }

    /// <summary>
    /// 支持图片输出.
    /// </summary>
    public bool ImageOutput { get; set; }

    /// <summary>
    /// 支持计算机视觉.
    /// </summary>
    public bool IsVision { get; set; }

    /// <summary>
    /// 是否开放给大家使用.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
