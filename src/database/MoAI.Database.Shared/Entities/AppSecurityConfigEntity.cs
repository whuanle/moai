using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 应用安全配置（内容脱敏），与 app 一一对应（Agent/流程应用通用），未配置行的应用视为未启用脱敏.
/// </summary>
public partial class AppSecurityConfigEntity : IFullAudited
{
    /// <summary>
    /// 配置ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 所属应用ID，逻辑关联app.id（1:1，不建物理外键）.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 是否启用内容脱敏；启用且存在至少一条规则时生效.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 是否对工具调用结果（含错误信息、流程节点输出）脱敏.
    /// </summary>
    public bool MaskToolResult { get; set; }

    /// <summary>
    /// 是否对工具调用参数（调用记录展示、流程节点输入）脱敏.
    /// </summary>
    public bool MaskToolArgs { get; set; }

    /// <summary>
    /// 是否对模型回复文本（对话正文）脱敏.
    /// </summary>
    public bool MaskModelOutput { get; set; }

    /// <summary>
    /// 内容脱敏规则列表（工具调用结果/工具调用参数范围共用），JSON 数组文本，元素为 {name,type,pattern,replacement}，type 见 AppSecurityRuleTypes，空为 &apos;[]&apos;.
    /// </summary>
    public string Rules { get; set; } = default!;

    /// <summary>
    /// 模型回复专属脱敏规则列表，与 Rules 相互独立维护，JSON 数组文本，元素同 Rules，空为 &apos;[]&apos;.
    /// </summary>
    public string ModelOutputRules { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除，0=未删除（legacy bigint 约定）.
    /// </summary>
    public long IsDeleted { get; set; }
}
