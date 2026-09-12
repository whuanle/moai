using MediatR;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询可挂载的技能选项列表（启用中的技能），登录用户可调用，
/// 供应用配置页选择挂载.
/// </summary>
public class QuerySkillOptionsCommand : IRequest<QuerySkillOptionsCommandResponse>
{
}
