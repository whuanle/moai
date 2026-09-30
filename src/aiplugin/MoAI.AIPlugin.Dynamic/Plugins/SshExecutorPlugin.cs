using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using Renci.SshNet;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// SSH 命令执行（动态插件）：对实例配置的目标主机执行白名单内的命令做远程巡检与处置，输出退出码与标准输出/错误.
/// </summary>
/// <remarks>
/// 处置类插件的护栏三件套（<see cref="SshCommandGuard"/>，校验先于建立连接）：
/// <list type="number">
/// <item><description>**白名单 fail-closed**：实例配置 CommandWhitelist（逗号分隔命令前缀）为空时拒绝一切命令；命令（含管道每一段）必须命中前缀。</description></item>
/// <item><description>**拼接/替换符拒绝**：; &amp;&amp; || &amp; 反引号 $( 换行 一律拒绝，防止把任意命令拼到白名单命令之后。</description></item>
/// <item><description>**灾难级黑名单**：mkfs/dd/shutdown/reboot/halt/poweroff/fdisk/parted/init，白名单亦不可放行。</description></item>
/// </list>
/// 鉴权支持密码或 PEM/OpenSSH 私钥（私钥优先）；命令超时由 SshCommand.CommandTimeout 约束。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件（每次新建 SSH 连接，用完即弃）。
/// </remarks>
[AiPlugin(
    key: "ssh_executor",
    Name = "SSH 命令执行",
    Description = "对目标主机执行白名单内的 SSH 命令做远程巡检与处置：命令（含管道每段）必须命中实例配置 CommandWhitelist 前缀，拼接/替换符与灾难级命令（mkfs/dd/reboot 等）一律拒绝；先以 {\"Command\":\"uptime\"} 起步")]
public class SshExecutorPlugin : IDynamicPluginRuntime<SshExecutorRequest, SshExecutorResponse, SshExecutorConfig>
{
    /// <summary>超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>输出字符数的下界.</summary>
    private const int MinMaxOutputChars = 256;

    /// <summary>输出字符数的上界.</summary>
    private const int MaxMaxOutputChars = 65536;

    /// <summary>端口的下界.</summary>
    private const int MinPort = 1;

    /// <summary>端口的上界.</summary>
    private const int MaxPort = 65535;

    private SshExecutorConfig _config = new();

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Command": "journalctl -u nginx --since \"1 hour ago\" | tail -n 100" // 单条命令；管道 | 可用（每段须命中白名单）
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "Host": "10.0.0.5",              // SSH 主机
              "Port": 22,                      // 端口，1-65535
              "Username": "ops",               // 登录用户名（建议权限受控的运维账号）
              "Password": "",                  // 密码（与私钥二选一）
              "PrivateKey": "",                // 私钥全文（PEM/OpenSSH，已填时优先于密码）
              "CommandWhitelist": "systemctl status,journalctl,df,free,uptime,tail,ps", // 命令前缀白名单（必填，逗号分隔）
              "CommandTimeoutSeconds": 30,     // 连接与命令超时秒数，1-300
              "MaxOutputChars": 8192           // 输出最大返回字符数，256-65536
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(SshExecutorConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            return Task.FromResult<string?>("SSH 主机 Host 不能为空");
        }

        if (config.Port < MinPort || config.Port > MaxPort)
        {
            return Task.FromResult<string?>($"SSH 端口必须在 1-65535，收到 {config.Port}");
        }

        if (string.IsNullOrWhiteSpace(config.Username))
        {
            return Task.FromResult<string?>("登录用户名 Username 不能为空");
        }

        _config = new SshExecutorConfig
        {
            Host = config.Host.Trim(),
            Port = config.Port,
            Username = config.Username.Trim(),
            Password = config.Password ?? string.Empty,
            PrivateKey = config.PrivateKey ?? string.Empty,
            CommandWhitelist = config.CommandWhitelist ?? string.Empty,
            CommandTimeoutSeconds = Math.Clamp(config.CommandTimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxOutputChars = Math.Clamp(config.MaxOutputChars, MinMaxOutputChars, MaxMaxOutputChars),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<SshExecutorResponse> RunAsync(SshExecutorRequest request, CancellationToken cancellationToken)
    {
        var command = (request.Command ?? string.Empty).Trim();
        var violation = SshCommandGuard.Validate(command, _config.CommandWhitelist);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        using var client = BuildClient();
        try
        {
            client.Connect();
        }
        catch (Renci.SshNet.Common.SshAuthenticationException ex)
        {
            throw new BusinessException(502, $"SSH 认证失败：{ex.Message}，请检查用户名/密码/私钥");
        }
        catch (Exception ex) when (ex is Renci.SshNet.Common.SshException or System.Net.Sockets.SocketException or System.IO.IOException)
        {
            throw new BusinessException(502, $"SSH 连接失败：{ex.Message}");
        }

        if (!client.IsConnected)
        {
            throw new BusinessException(502, "SSH 连接失败：无法建立会话，请检查主机、端口与网络");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var sshCommand = client.RunCommand(command);
            sshCommand.CommandTimeout = TimeSpan.FromSeconds(_config.CommandTimeoutSeconds);
            var task = Task.Run(sshCommand.Execute, cancellationToken);
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return new SshExecutorResponse
            {
                Command = command,
                ExitStatus = sshCommand.ExitStatus,
                Ok = sshCommand.ExitStatus == 0,
                Output = ObservabilityJson.Truncate(sshCommand.Result ?? string.Empty, _config.MaxOutputChars),
                ErrorOutput = ObservabilityJson.Truncate(sshCommand.Error ?? string.Empty, _config.MaxOutputChars),
                DurationMs = stopwatch.ElapsedMilliseconds,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(502, $"SSH 命令超时（{_config.CommandTimeoutSeconds}s）");
        }
        catch (Renci.SshNet.Common.SshException ex)
        {
            throw new BusinessException(502, $"SSH 执行失败：{ex.Message}");
        }
    }

    private SshClient BuildClient()
    {
        var client = _config.PrivateKey.Length > 0
            ? new SshClient(_config.Host, _config.Port, _config.Username, ReadPrivateKey(_config.PrivateKey))
            : new SshClient(_config.Host, _config.Port, _config.Username, _config.Password);
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(_config.CommandTimeoutSeconds);
        return client;
    }

    private static PrivateKeyFile ReadPrivateKey(string privateKey)
    {
        try
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKey));
            return new PrivateKeyFile(stream);
        }
        catch (Exception ex)
        {
            throw new BusinessException(400, $"私钥解析失败：{ex.Message}（请确认私钥为 PEM/OpenSSH 无口令格式）");
        }
    }
}
