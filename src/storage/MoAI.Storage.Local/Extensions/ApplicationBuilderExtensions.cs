using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MoAI.Infra;

namespace MoAI.Storage.Extensions;

/// <summary>
/// ApplicationBuilderExtensions.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 配置使用本地文件.
    /// </summary>
    /// <param name="builder"></param>
    public static void UseLocalFiles(this IApplicationBuilder builder)
    {
        var systemOptions = builder.ApplicationServices.GetRequiredService<SystemOptions>();

        var filePath = Path.Combine(systemOptions.Storage.LocalPath, "public");

        // 登录后就能直接看到的文件
        builder.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(filePath),
            RequestPath = "/download/public",
        });
    }
}
