namespace MoAI.Common.Queries.Response;

/// <summary>
/// 服务器信息.
/// </summary>
public class QueryServerInfoCommandResponse
{
    /// <summary>
    /// 网站名称.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// 网站 Logo（存储中的 ObjectKey，空表示使用前端默认 Logo）.
    /// </summary>
    public string LogoPath { get; init; } = string.Empty;

    /// <summary>
    /// 登录页背景图（存储中的 ObjectKey，空表示使用前端默认背景）.
    /// </summary>
    public string LoginBackgroundPath { get; init; } = string.Empty;

    /// <summary>
    /// 系统访问地址.
    /// </summary>
    public string ServiceUrl { get; init; } = default!;

    /// <summary>
    /// 公共存储地址，静态资源时可直接访问.
    /// </summary>
    public string PublicStoreUrl { get; init; } = default!;

    /// <summary>
    /// RSA 公钥，用于加密密码等信息传输到服务器.
    /// </summary>
    public string RsaPublic { get; init; } = default!;

    /// <summary>
    /// 最大可上传文件大小，单位为字节.
    /// </summary>
    public int MaxUploadFileSize { get; init; }

    /// <summary>
    /// 注册是否需要邮箱验证码（SMTP 已启用且注册验证码开关开启）.
    /// </summary>
    public bool RegisterEmailCodeRequired { get; init; }

    /// <summary>
    /// 是否允许通过邮箱找回密码（SMTP 邮件服务已启用；未启用时由管理员重置密码）.
    /// </summary>
    public bool PasswordResetEnabled { get; init; }
}