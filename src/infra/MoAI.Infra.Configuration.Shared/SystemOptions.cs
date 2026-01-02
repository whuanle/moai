using MoAI.Infra.Models;

namespace MoAI.Infra;

/// <summary>
/// 系统配置.
/// </summary>
public class SystemOptions
{
    /// <summary>
    /// 开启调试输出.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// 系统名称.
    /// </summary>
    public string Name { get; init; } = "MoAI";

    /// <summary>
    /// 监听端口.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// 服务访问地址.
    /// </summary>
    public string Server { get; init; } = string.Empty;

    /// <summary>
    /// 前端地址.
    /// </summary>
    public string WebUI { get; init; } = string.Empty;

    /// <summary>
    /// 系统数据库类型.
    /// </summary>
    public string DBType { get; init; } = string.Empty;

    /// <summary>
    /// 系统数据库连接字符串.
    /// </summary>
    public string Database { get; init; } = string.Empty;

    /// <summary>
    /// Redis 连接字符串.
    /// </summary>
    public string Redis { get; init; } = string.Empty;

    /// <summary>
    /// RabbitMQ 连接字符串.
    /// </summary>
    public string RabbitMQ { get; init; } = string.Empty;

    /// <summary>
    /// 文档向量化存储.
    /// </summary>
    public DatabaseStorage Wiki { get; init; } = new DatabaseStorage();

    /// <summary>
    /// Storage.
    /// </summary>
    public required SystemOptionStorage Storage { get; init; }

    /// <summary>
    /// 最大上传文件大小，单位为字节，默认 100MB.
    /// </summary>
    public int MaxUploadFileSize { get; init; } = 1024 * 1024 * 100;
}

public class SystemOptionStorage
{
    /// <summary>
    /// 存储类型，S3、Local，不填写默认 Local
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 本地路径.
    /// </summary>
    public string LocalPath { get; init; } = string.Empty;

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