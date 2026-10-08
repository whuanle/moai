using MoAI.Database.Aggregates;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用安全配置响应.
/// </summary>
public class QueryAppSecurityCommandResponse
{
    /// <summary>
    /// 应用 id.
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
    /// 内容脱敏规则列表（工具调用结果/工具调用参数范围共用），未配置为空列表.
    /// </summary>
    public IReadOnlyList<AppSecurityPolicy.AppSecurityRule> Rules { get; set; } = [];

    /// <summary>
    /// 模型回复专属脱敏规则列表，与 <see cref="Rules"/> 相互独立维护，未配置为空列表.
    /// </summary>
    public IReadOnlyList<AppSecurityPolicy.AppSecurityRule> ModelOutputRules { get; set; } = [];

    /// <summary>
    /// 当前用户在应用所属团队中的角色（0=成员 1=管理员 2=所有者）.
    /// </summary>
    public int MyRole { get; set; }
}
