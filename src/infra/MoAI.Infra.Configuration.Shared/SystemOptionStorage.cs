using Microsoft.AspNetCore.Mvc.Formatters;
using MoAI.Infra.Models;

namespace MoAI.Infra;

/// <summary>
/// 存储器配置.
/// </summary>
public class SystemOptionStorage
{
    /// <summary>
    /// S3 服务器地址.
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// 强制使用后缀路径样式.
    /// </summary>
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>
    /// 存储桶名称.
    /// </summary>
    public string Bucket { get; init; } = string.Empty;

    /// <summary>
    /// id.
    /// </summary>
    public string AccessKeyId { get; init; } = string.Empty;

    /// <summary>
    /// key.
    /// </summary>
    public string AccessKeySecret { get; init; } = string.Empty;
}
