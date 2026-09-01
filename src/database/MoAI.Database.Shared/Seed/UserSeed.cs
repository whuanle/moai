using System;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;
using MoAI.Infra.Helpers;

namespace MoAI.Database.Seed;

/// <summary>
/// 用户种子数据.
/// </summary>
public static class UserSeed
{
    private const string DefaultPassword = "YWJjZDEyMzQ1Ng==";

    /// <summary>
    /// 应用用户种子数据.
    /// </summary>
    /// <param name="modelBuilder">模型构建器.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        var (hashPassword, salt) = PBKDF2Helper.ToHash(Encoding.UTF8.GetString(Convert.FromBase64String(DefaultPassword)));

        modelBuilder.Entity<UserEntity>().HasData(
            new UserEntity
            {
                Id = 1,
                UserName = "admin",
                NickName = "admin",
                Email = "admin@admin.com",
                Password = hashPassword,
                PasswordSalt = salt,
                Phone = "12345678901",
                IsAdmin = true,
                AvatarPath = string.Empty
            });
    }
}
