using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// 系统级接入头像公共校验：仅允许引用已完成上传并登记的文件，防止任意伪造 objectKey（与插件/用户头像同规则）.
/// </summary>
internal static class SubsysAvatarGuard
{
    /// <summary>
    /// 校验头像 objectKey 对应文件已上传完成；空/缺省原样返回空串（未设置）.
    /// </summary>
    /// <param name="avatarPath">头像 objectKey，可为空.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回规范化的 objectKey（空串=未设置）.</returns>
    public static async Task<string> NormalizeAsync(string? avatarPath, DatabaseContext databaseContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(avatarPath))
        {
            return string.Empty;
        }

        var fileExists = await databaseContext.Files
            .AnyAsync(f => f.ObjectKey == avatarPath && f.IsUploaded, cancellationToken);
        if (!fileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
        }

        return avatarPath;
    }
}
