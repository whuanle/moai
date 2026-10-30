using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.Org.Handlers;

/// <summary>
/// 组织角色树公共校验：上级角色存在性、同父重名、防环与路径长度.
/// </summary>
internal static class OrgRoleTreeGuard
{
    /// <summary>
    /// 物化路径列最大长度.
    /// </summary>
    public const int MaxPathLength = 255;

    /// <summary>
    /// 加载上级角色，不存在时抛 404；parentId 为 null 返回 null（根）.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="parentId">上级角色 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>上级角色实体，根角色返回 null.</returns>
    public static async Task<OrgRoleEntity?> FindParentAsync(DatabaseContext databaseContext, int? parentId, CancellationToken cancellationToken)
    {
        if (parentId == null)
        {
            return null;
        }

        var parent = await databaseContext.OrgRoles
            .FirstOrDefaultAsync(x => x.Id == parentId.Value, cancellationToken);
        if (parent == null)
        {
            throw new BusinessException("上级角色不存在.") { StatusCode = 404 };
        }

        return parent;
    }

    /// <summary>
    /// 校验同一上级下未重名（excludeRoleId 用于更新场景排除自身）.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="parentId">上级角色 id（null=根层）.</param>
    /// <param name="name">角色名称.</param>
    /// <param name="excludeRoleId">更新时排除的角色 id，可空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public static async Task EnsureNoDuplicateNameAsync(DatabaseContext databaseContext, int? parentId, string name, int? excludeRoleId, CancellationToken cancellationToken)
    {
        var duplicated = await databaseContext.OrgRoles
            .AnyAsync(x => x.ParentId == parentId && x.Name == name && (excludeRoleId == null || x.Id != excludeRoleId), cancellationToken);
        if (duplicated)
        {
            throw new BusinessException("同一上级下已存在同名角色.") { StatusCode = 409 };
        }
    }

    /// <summary>
    /// 校验新路径不超列长：新父 path + 本角色段 + 最深后代相对后缀.
    /// </summary>
    /// <param name="newParentPath">新父角色 path，根为 "/".</param>
    /// <param name="roleId">被移动角色 id.</param>
    /// <param name="maxDescendantSuffixLength">子树内最深后代相对后缀长度（相对被移动角色原 path）.</param>
    public static void EnsurePathLength(string newParentPath, int roleId, int maxDescendantSuffixLength)
    {
        var newPathLength = newParentPath.Length + roleId.ToString().Length + 1 + maxDescendantSuffixLength;
        if (newPathLength > MaxPathLength)
        {
            throw new BusinessException("角色层级过深，超出路径长度限制.") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 拼接角色 path：父 path（根为 "/"）+ 角色 id + "/".
    /// </summary>
    /// <param name="parentPath">父角色 path，根为 "/".</param>
    /// <param name="roleId">角色 id.</param>
    /// <returns>角色物化路径.</returns>
    public static string BuildPath(string parentPath, int roleId)
        => $"{parentPath}{roleId}/";

    /// <summary>
    /// path 层级序比较：按 "/" 分段数值比较（"/7/" 与 "/10/" 数值序正确，纯字符串序会因位数不同乱序）.
    /// </summary>
    /// <param name="left">左 path.</param>
    /// <param name="right">右 path.</param>
    /// <returns>比较结果，负数表示 left 靠前.</returns>
    public static int ComparePath(string left, string right)
    {
        var leftSegments = left.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rightSegments = right.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var length = Math.Min(leftSegments.Length, rightSegments.Length);
        for (var i = 0; i < length; i++)
        {
            var compare = int.Parse(leftSegments[i]).CompareTo(int.Parse(rightSegments[i]));
            if (compare != 0)
            {
                return compare;
            }
        }

        return leftSegments.Length.CompareTo(rightSegments.Length);
    }
}
