using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 团队智能体，主智能体 + 多个子智能体的编排定义资产，供应用绑定后运行.
/// </summary>
public partial class TeamAgentEntity : IFullAudited
{
    /// <summary>
    /// 智能体ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联team.id（仓库约定不建物理外键）.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 智能体标识 key，团队内唯一（小写字母开头的字母/数字/下划线），创建后不可变更；Phase 2 将作为工具名暴露.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 智能体名称（展示名，可中文），团队内唯一.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 智能体描述，空串=未填写.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 头像文件 objectKey，空串=未设置.
    /// </summary>
    public string AvatarObjectKey { get; set; } = default!;

    /// <summary>
    /// 是否启用，停用后不可被应用绑定与运行.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 是否系统内置智能体：团队创建时自动种入（如规划/构建），不可删除、可编辑定制.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// 主智能体系统提示词.
    /// </summary>
    public string MainPrompt { get; set; } = default!;

    /// <summary>
    /// 主智能体插件依赖，JSON uuid 字符串数组.
    /// </summary>
    public string MainPluginIds { get; set; } = default!;

    /// <summary>
    /// 主智能体知识库依赖，JSON 数字数组.
    /// </summary>
    public string MainWikiIds { get; set; } = default!;

    /// <summary>
    /// 主智能体知识图谱依赖，JSON 数字数组.
    /// </summary>
    public string MainGraphIds { get; set; } = default!;

    /// <summary>
    /// 子智能体列表，JSON 数组（key/name/description/prompt/pluginIds/wikiIds/graphIds），最多 20 个.
    /// </summary>
    public string Subs { get; set; } = default!;

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }

    /// <summary>
    /// 创建人用户ID，审计钩子插入时自动填充.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间，审计钩子自动填充，默认timezone(utc,now()).
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人用户ID，审计钩子更新/删除时自动填充.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间，审计钩子插入/更新/删除时自动刷新.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
