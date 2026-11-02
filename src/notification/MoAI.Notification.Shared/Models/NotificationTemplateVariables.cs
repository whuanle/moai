using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoAI.Notification.Models;

/// <summary>
/// 通知模板变量定义：模板正文中 <c>{name}</c> 占位符的说明，发送时按 name 提供变量值.
/// </summary>
public class NotificationTemplateVariable
{
    /// <summary>
    /// 变量名（字母开头，仅字母/数字/下划线，模板内唯一）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 变量说明.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 示例值（便于复制到外部平台提审时对照）.
    /// </summary>
    public string Example { get; init; } = string.Empty;
}

/// <summary>
/// 模板变量定义 JSON 解析与校验.
/// </summary>
public static class NotificationTemplateVariableParser
{
    /// <summary>
    /// 变量名规则：字母开头，字母/数字/下划线.
    /// </summary>
    public static readonly Regex NameRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// 解析变量定义 JSON，返回变量列表；空串视为无变量.
    /// </summary>
    /// <param name="variablesJson">变量定义 JSON.</param>
    /// <param name="error">解析失败时的错误消息.</param>
    /// <returns>解析失败返回 null.</returns>
    public static List<NotificationTemplateVariable>? Parse(string? variablesJson, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return new List<NotificationTemplateVariable>();
        }

        try
        {
            var variables = JsonSerializer.Deserialize<List<NotificationTemplateVariable>>(variablesJson, NotificationChannelConfigs.JsonOptions);
            if (variables == null)
            {
                error = "变量定义不是有效的 JSON 数组";
                return null;
            }

            var seen = new HashSet<string>();
            foreach (var variable in variables)
            {
                if (!NameRegex.IsMatch(variable.Name))
                {
                    error = $"变量名不合法：{variable.Name}（需字母开头，仅字母/数字/下划线）";
                    return null;
                }

                if (!seen.Add(variable.Name))
                {
                    error = $"变量名重复：{variable.Name}";
                    return null;
                }
            }

            return variables;
        }
        catch (JsonException)
        {
            error = "变量定义不是有效的 JSON 数组";
            return null;
        }
    }

    /// <summary>
    /// 序列化变量定义为 JSON.
    /// </summary>
    /// <param name="variables">变量列表.</param>
    /// <returns>返回 JSON.</returns>
    public static string Serialize(IReadOnlyList<NotificationTemplateVariable> variables)
        => JsonSerializer.Serialize(variables, NotificationChannelConfigs.JsonOptions);

    /// <summary>
    /// 校验发送方提供的变量值是否覆盖模板全部变量，返回缺失变量名列表.
    /// </summary>
    /// <param name="defined">模板定义的变量.</param>
    /// <param name="values">发送方提供的变量值.</param>
    /// <returns>缺失的变量名（空表示通过）.</returns>
    public static List<string> MissingValues(IReadOnlyList<NotificationTemplateVariable> defined, IReadOnlyDictionary<string, string>? values)
    {
        return defined.Where(x => values == null || !values.ContainsKey(x.Name)).Select(x => x.Name).ToList();
    }
}
