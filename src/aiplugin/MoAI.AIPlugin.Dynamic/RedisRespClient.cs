using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 极简 Redis RESP2 客户端（每次运行新建连接，用完即弃）：只服务诊断插件的白名单只读命令，
/// 不做能力探测与常驻心跳，因此对自建桩与老版本 Redis 都保持最小协议面.
/// </summary>
/// <remarks>
/// 回复解码形态：简单字符串/批量字符串 → <see cref="string"/>，整数 → <see cref="long"/>，
/// 数组 → <c>List&lt;object?&gt;</c>，nil → null；错误回复直接抛 <see cref="BusinessException"/>.
/// </remarks>
internal sealed class RedisRespClient : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly bool _ssl;
    private readonly TimeSpan _timeout;

    private TcpClient? _client;
    private Stream? _stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRespClient"/> class.
    /// </summary>
    /// <param name="host">主机.</param>
    /// <param name="port">端口.</param>
    /// <param name="ssl">是否 TLS.</param>
    /// <param name="timeoutSeconds">连接与读写超时秒数.</param>
    public RedisRespClient(string host, int port, bool ssl, int timeoutSeconds)
    {
        _host = host;
        _port = port;
        _ssl = ssl;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    /// <summary>
    /// 建立连接并完成 AUTH/SELECT（仅在有配置时下发）.
    /// </summary>
    /// <param name="username">ACL 用户名（可空）.</param>
    /// <param name="password">密码（可空）.</param>
    /// <param name="database">逻辑库（0 跳过 SELECT）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>任务.</returns>
    public async Task ConnectAsync(string? username, string? password, int database, CancellationToken cancellationToken)
    {
        _client = new TcpClient();
        using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            connectTimeout.CancelAfter(_timeout);
            await _client.ConnectAsync(_host, _port, connectTimeout.Token).ConfigureAwait(false);
        }

        Stream raw = _client.GetStream();
        if (_ssl)
        {
            var sslStream = new SslStream(raw);
            using (var sslTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                sslTimeout.CancelAfter(_timeout);
                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = _host }, sslTimeout.Token).ConfigureAwait(false);
            }

            _stream = sslStream;
        }
        else
        {
            _stream = raw;
        }

        if (!string.IsNullOrEmpty(password))
        {
            if (string.IsNullOrEmpty(username))
            {
                await ExecAsync("AUTH", password, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecAsync("AUTH", username, password, cancellationToken).ConfigureAwait(false);
            }
        }

        if (database > 0)
        {
            await ExecAsync("SELECT", database.ToString(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 下发一条命令并解码回复.
    /// </summary>
    /// <param name="command">命令名.</param>
    /// <param name="args">参数（字符串）.</param>
    /// <returns>解码后的回复（形态见类型注释）.</returns>
    public async Task<object?> ExecAsync(string command, params object[] args)
    {
        return await ExecAsync(command, args, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 下发一条命令并解码回复（带取消令牌）.
    /// </summary>
    /// <param name="command">命令名.</param>
    /// <param name="args">参数（字符串）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>解码后的回复.</returns>
    public async Task<object?> ExecAsync(string command, object[] args, CancellationToken cancellationToken)
    {
        var payload = new StringBuilder();
        payload.Append('*').Append(args.Length + 1).Append("\r\n");
        AppendBulk(payload, command);
        foreach (var arg in args)
        {
            AppendBulk(payload, arg?.ToString() ?? string.Empty);
        }

        var bytes = Encoding.UTF8.GetBytes(payload.ToString());
        using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        writeTimeout.CancelAfter(_timeout);
        await _stream!.WriteAsync(bytes, writeTimeout.Token).ConfigureAwait(false);
        return await ReadReplyAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static void AppendBulk(StringBuilder payload, string value)
    {
        payload.Append('$').Append(Encoding.UTF8.GetByteCount(value)).Append("\r\n").Append(value).Append("\r\n");
    }

    /// <summary>
    /// 读取并解码一条回复（按 RESP2 首字节分派）.
    /// </summary>
    private async Task<object?> ReadReplyAsync(string command, CancellationToken cancellationToken)
    {
        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readTimeout.CancelAfter(_timeout);
        var type = await ReadByteAsync(readTimeout.Token).ConfigureAwait(false);
        switch (type)
        {
            case '+': // 简单字符串
                return await ReadLineAsync(readTimeout.Token).ConfigureAwait(false);
            case '-': // 错误回复
                var error = await ReadLineAsync(readTimeout.Token).ConfigureAwait(false);
                throw new BusinessException(502, $"Redis 命令 {command} 失败：{error}");
            case ':': // 整数
                var integerValue = await ReadLineAsync(readTimeout.Token).ConfigureAwait(false);
                return long.TryParse(integerValue, out var parsed) ? parsed : 0L;
            case '$': // 批量字符串
                var length = await ReadLineAsync(readTimeout.Token).ConfigureAwait(false);
                var bulkLength = int.Parse(length, System.Globalization.CultureInfo.InvariantCulture);
                if (bulkLength < 0)
                {
                    return null;
                }

                var bulk = await ReadExactAsync(bulkLength, readTimeout.Token).ConfigureAwait(false);
                await ExpectCrlfAsync(readTimeout.Token).ConfigureAwait(false);
                return Encoding.UTF8.GetString(bulk);
            case '*': // 数组
                var countLine = await ReadLineAsync(readTimeout.Token).ConfigureAwait(false);
                var count = int.Parse(countLine, System.Globalization.CultureInfo.InvariantCulture);
                if (count < 0)
                {
                    return null;
                }

                var items = new System.Collections.Generic.List<object?>(count);
                for (var i = 0; i < count; i++)
                {
                    items.Add(await ReadReplyAsync(command, cancellationToken).ConfigureAwait(false));
                }

                return items;
            default:
                throw new BusinessException(502, $"Redis 响应形态未知（0x{(type >= 0 ? type : -1):X2}），请检查目标是否为 Redis 服务");
        }
    }

    private async Task<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var read = await _stream!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return read == 0 ? throw new BusinessException(502, "Redis 连接被对端关闭") : buffer[0];
    }

    private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        var bytes = new System.Collections.Generic.List<byte>();
        while (true)
        {
            var value = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (value == '\n')
            {
                break;
            }

            if (value != '\r')
            {
                bytes.Add((byte)value);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await _stream!.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new BusinessException(502, "Redis 连接被对端关闭");
            }

            offset += read;
        }

        return buffer;
    }

    private async Task ExpectCrlfAsync(CancellationToken cancellationToken)
    {
        var cr = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        var lf = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        if (cr != '\r' || lf != '\n')
        {
            throw new BusinessException(502, "Redis 响应批量数据结尾缺少 CRLF");
        }
    }
}
