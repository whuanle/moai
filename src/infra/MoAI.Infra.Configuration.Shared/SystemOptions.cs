using Microsoft.AspNetCore.Mvc.Formatters;
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
    /// 系统加密.
    /// </summary>
    public string AES { get; init; }

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
    /// Storage.
    /// </summary>
    public required SystemOptionStorage Storage { get; init; }

    /// <summary>
    /// 最大上传文件大小，单位为字节，默认 100MB.
    /// </summary>
    public int MaxUploadFileSize { get; init; } = 1024 * 1024 * 100;

    /// <summary>
    /// 可观察性.
    /// </summary>
    public OpenTelemetryOptions OTLP { get; init; } = new();
}
