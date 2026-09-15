using FluentValidation;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 实体类型属性定义共用校验规则（创建/更新实体类型命令共用）.
/// </summary>
public static class EntityTypePropertyRules
{
    /// <summary>
    /// 应用属性定义校验：数量、名称、类型、描述.
    /// </summary>
    /// <typeparam name="T">命令类型.</typeparam>
    /// <param name="rule">属性列表规则.</param>
    public static void Apply<T>(IRuleBuilder<T, List<KnowledgeGraphEntityTypeProperty>> rule)
    {
        rule
            .Must(x => x.Count <= 50).WithMessage("属性定义最多 50 个.")
            .Must(x => x.All(p => !string.IsNullOrWhiteSpace(p.Name) && p.Name.Length <= 100)).WithMessage("属性名不能为空且最长 100 个字符.")
            .Must(x => x.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == x.Count).WithMessage("属性名不能重复.")
            .Must(x => x.All(p => KnowledgeGraphEntityTypeProperty.AllowedTypes.Contains(p.Type, StringComparer.Ordinal))).WithMessage("属性类型只支持 string/number/boolean/date.")
            .Must(x => x.All(p => p.Description.Length <= 255)).WithMessage("属性描述最长 255 个字符.");
    }
}
