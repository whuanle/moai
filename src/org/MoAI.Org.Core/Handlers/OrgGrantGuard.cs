using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;

namespace MoAI.Org.Handlers;

/// <summary>
/// 组织授权参数守卫：路由/查询串中的资源类型字符串解析并校验.
/// </summary>
public static class OrgGrantGuard
{
    /// <summary>
    /// 解析资源类型字符串，不合法抛 400.
    /// </summary>
    /// <param name="value">枚举名或 json 名.</param>
    /// <returns>资源类型枚举.</returns>
    public static OrgGrantResourceType ParseResourceType(string? value)
    {
        if (!OrgGrantResourceTypes.TryParse(value, out var resourceType))
        {
            throw new BusinessException("资源类型不合法.") { StatusCode = 400 };
        }

        return resourceType;
    }
}
