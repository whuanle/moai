using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.Database;
using OpenSandbox;
using OpenSandbox.CodeInterpreter;
using OpenSandbox.CodeInterpreter.Models;
using OpenSandbox.Config;
using OpenSandbox.Core;
using OpenSandbox.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;
using Execution = OpenSandbox.Models.Execution;

namespace MoAI.AI.Services;

/// <summary>
/// 基于 OpenSandbox 的会话沙箱服务：每会话一沙箱，惰性创建、按需续期、TTL 回收.
/// </summary>
[InjectOnScoped]
public sealed class OpenSandboxService : IAppSandboxService
{
    private const string CodeInterpreterEntrypoint = "/opt/code-interpreter/code-interpreter.sh";
    private const string MetadataSessionKey = "moai.session";
    private const string MetadataAppKey = "moai.app";
    private const string MetadataTeamKey = "moai.team";

    /// <summary>
    /// 每会话创建锁（跨作用域共享，避免并发工具调用重复建沙箱）.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CreateLocks = new();

    private readonly MoAI.Infra.SystemOptions _options;
    private readonly IRedisDatabase _redisDatabase;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<OpenSandboxService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenSandboxService"/> class.
    /// </summary>
    /// <param name="options">系统配置.</param>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="logger">日志.</param>
    public OpenSandboxService(
        MoAI.Infra.SystemOptions options,
        IRedisDatabase redisDatabase,
        DatabaseContext databaseContext,
        ILogger<OpenSandboxService> logger)
    {
        _options = options;
        _redisDatabase = redisDatabase;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<string> RunCodeAsync(SandboxSessionContext context, string? language, string code, CancellationToken cancellationToken)
    {
        var effectiveLanguage = string.IsNullOrWhiteSpace(language) ? SupportedLanguage.Python : language!.Trim().ToLowerInvariant();
        return WithInterpreterAsync(context, async (interpreter, ct) =>
        {
            var execution = await interpreter.Codes.RunAsync(code, new RunCodeOptions { Language = effectiveLanguage }, ct).ConfigureAwait(false);
            return MapExecution(execution);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> RunShellAsync(SandboxSessionContext context, string command, string? workingDirectory, CancellationToken cancellationToken)
        => WithSandboxAsync(context, async (sandbox, ct) =>
        {
            var options = string.IsNullOrWhiteSpace(workingDirectory) ? null : new RunCommandOptions { WorkingDirectory = workingDirectory };
            var execution = await sandbox.Commands.RunAsync(command, options, cancellationToken: ct).ConfigureAwait(false);
            return MapExecution(execution);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<string> WriteFileAsync(SandboxSessionContext context, string path, string content, CancellationToken cancellationToken)
        => WithSandboxAsync(context, async (sandbox, ct) =>
        {
            await sandbox.Files.WriteFilesAsync([new WriteEntry { Path = path, Data = content }], ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, path }, JsonOptions);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<string> ReadFileAsync(SandboxSessionContext context, string path, CancellationToken cancellationToken)
        => WithSandboxAsync(context, async (sandbox, ct) =>
        {
            var content = await sandbox.Files.ReadFileAsync(path, cancellationToken: ct).ConfigureAwait(false);
            return content;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<byte[]> ReadFileBytesAsync(SandboxSessionContext context, string path, CancellationToken cancellationToken)
        => WithSandboxAsync(context, async (sandbox, ct) =>
        {
            return await sandbox.Files.ReadBytesAsync(path, cancellationToken: ct).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<string> ListDirectoryAsync(SandboxSessionContext context, string? path, int? depth, CancellationToken cancellationToken)
        => WithSandboxAsync(context, async (sandbox, ct) =>
        {
            var effectivePath = string.IsNullOrWhiteSpace(path) ? "/" : path!;
            var entries = await sandbox.Files.ListDirectoryAsync(effectivePath, depth, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                path = effectivePath,
                entries = entries.Select(e => new { path = e.Path, type = e.Type, size = e.Size, modifiedAt = e.ModifiedAt }),
            }, JsonOptions);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<string> DeleteFileAsync(SandboxSessionContext context, string path, CancellationToken cancellationToken)
        => WithSandboxAsync(context, async (sandbox, ct) =>
        {
            await sandbox.Files.DeleteFilesAsync([path], ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, path }, JsonOptions);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<string> SearchFilesAsync(SandboxSessionContext context, string path, string pattern, CancellationToken cancellationToken)
        => WithSandboxAsync(context, async (sandbox, ct) =>
        {
            var results = await sandbox.Files.SearchAsync(new SearchEntry { Path = path, Pattern = pattern }, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                entries = results.Select(e => new { path = e.Path, type = e.Type, size = e.Size }),
            }, JsonOptions);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task KillSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entry = await GetEntryAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (entry == null)
        {
            return;
        }

        await using var manager = CreateManager();
        try
        {
            await manager.KillSandboxAsync(entry.SandboxId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "销毁沙箱 {SandboxId} 失败（可能已回收）.", entry.SandboxId);
        }

        await RemoveEntryAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> ReapOrphansAsync(CancellationToken cancellationToken = default)
    {
        await using var manager = CreateManager();
        var response = await manager.ListSandboxInfosAsync(new SandboxFilter(), cancellationToken).ConfigureAwait(false);
        var ours = (response.Items ?? [])
            .Where(x => x.Metadata != null && x.Metadata.ContainsKey(MetadataSessionKey))
            .ToList();

        if (ours.Count == 0)
        {
            return 0;
        }

        var sessionIds = ours
            .Select(x => Guid.TryParse(x.Metadata![MetadataSessionKey], out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var aliveSessions = await _databaseContext.AppAgentSessions
            .Where(x => sessionIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var aliveSet = aliveSessions.ToHashSet();

        var killed = 0;
        foreach (var sandbox in ours)
        {
            if (!Guid.TryParse(sandbox.Metadata![MetadataSessionKey], out var sessionId) || !aliveSet.Contains(sessionId))
            {
                try
                {
                    await manager.KillSandboxAsync(sandbox.Id, cancellationToken).ConfigureAwait(false);
                    killed++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "回收孤儿沙箱 {SandboxId} 失败.", sandbox.Id);
                }
            }
        }

        return killed;
    }

    private async Task<T> WithSandboxAsync<T>(SandboxSessionContext context, Func<Sandbox, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var entry = await GetOrCreateEntryAsync(context, cancellationToken).ConfigureAwait(false);
        try
        {
            return await ConnectAndRunAsync(entry.SandboxId, action, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "沙箱 {SandboxId} 不可用，重建后重试.", entry.SandboxId);
            await RemoveEntryAsync(context.SessionId, cancellationToken).ConfigureAwait(false);
            var recreated = await CreateEntryAsync(context, cancellationToken).ConfigureAwait(false);
            return await ConnectAndRunAsync(recreated.SandboxId, action, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> WithInterpreterAsync(SandboxSessionContext context, Func<CodeInterpreter, CancellationToken, Task<string>> action, CancellationToken cancellationToken)
    {
        var entry = await GetOrCreateEntryAsync(context, cancellationToken).ConfigureAwait(false);
        try
        {
            return await ConnectInterpreterAndRunAsync(entry.SandboxId, action, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "沙箱 {SandboxId} 代码解释器不可用，重建后重试.", entry.SandboxId);
            await RemoveEntryAsync(context.SessionId, cancellationToken).ConfigureAwait(false);
            var recreated = await CreateEntryAsync(context, cancellationToken).ConfigureAwait(false);
            return await ConnectInterpreterAndRunAsync(recreated.SandboxId, action, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> ConnectAndRunAsync<T>(string sandboxId, Func<Sandbox, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        await using var sandbox = await Sandbox.ConnectAsync(
            new SandboxConnectOptions { ConnectionConfig = BuildConnectionConfig(), SandboxId = sandboxId },
            cancellationToken).ConfigureAwait(false);
        return await action(sandbox, cancellationToken);
    }

    private async Task<string> ConnectInterpreterAndRunAsync(string sandboxId, Func<CodeInterpreter, CancellationToken, Task<string>> action, CancellationToken cancellationToken)
    {
        await using var sandbox = await Sandbox.ConnectAsync(
            new SandboxConnectOptions { ConnectionConfig = BuildConnectionConfig(), SandboxId = sandboxId },
            cancellationToken).ConfigureAwait(false);
        var interpreter = await CodeInterpreter.CreateAsync(sandbox, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await action(interpreter, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SandboxCacheEntry> GetOrCreateEntryAsync(SandboxSessionContext context, CancellationToken cancellationToken)
    {
        var entry = await GetEntryAsync(context.SessionId, cancellationToken).ConfigureAwait(false);
        if (entry != null && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            var remaining = entry.ExpiresAt - DateTimeOffset.UtcNow;
            if (context.Settings.RenewOnAccess && remaining < TimeSpan.FromSeconds(Math.Max(30, _options.OpenSandBox.RenewThresholdSeconds)))
            {
                var timeout = ResolveTimeoutSeconds(context.Settings);
                await RenewAsync(entry.SandboxId, timeout, cancellationToken).ConfigureAwait(false);
                return new SandboxCacheEntry { SandboxId = entry.SandboxId, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(timeout) };
            }

            return entry;
        }

        return await CreateEntryAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SandboxCacheEntry> CreateEntryAsync(SandboxSessionContext context, CancellationToken cancellationToken)
    {
        var gate = CreateLocks.GetOrAdd(context.SessionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 双重检查：等待期间可能已由其他调用创建
            var existing = await GetEntryAsync(context.SessionId, cancellationToken).ConfigureAwait(false);
            if (existing != null && existing.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return existing;
            }

            var timeout = ResolveTimeoutSeconds(context.Settings);
            var image = _options.OpenSandBox.Image;

            await using var sandbox = await Sandbox.CreateAsync(new SandboxCreateOptions
            {
                ConnectionConfig = BuildConnectionConfig(),
                Image = image,
                Entrypoint = [CodeInterpreterEntrypoint],
                TimeoutSeconds = timeout,
                ReadyTimeoutSeconds = 300,
                Resource = BuildResource(context.Settings),
                NetworkPolicy = BuildNetworkPolicy(context.Settings),
                Metadata = new Dictionary<string, string>
                {
                    [MetadataSessionKey] = context.SessionId.ToString("D"),
                    [MetadataAppKey] = context.AppId.ToString("D"),
                    [MetadataTeamKey] = context.TeamId.ToString(),
                },
            }, cancellationToken).ConfigureAwait(false);

            var entry = new SandboxCacheEntry
            {
                SandboxId = sandbox.Id,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(timeout),
            };

            // Redis TTL 略长于沙箱存活时间，确保映射先于沙箱过期
            await WriteEntryAsync(context.SessionId, entry, TimeSpan.FromSeconds(timeout + 120), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("已为会话 {SessionId} 创建沙箱 {SandboxId}（{Image}）.", context.SessionId, sandbox.Id, image);
            return entry;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RenewAsync(string sandboxId, int timeoutSeconds, CancellationToken cancellationToken)
    {
        await using var manager = CreateManager();
        try
        {
            await manager.RenewSandboxAsync(sandboxId, timeoutSeconds, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "续期沙箱 {SandboxId} 失败.", sandboxId);
        }
    }

    private SandboxManager CreateManager()
        => SandboxManager.Create(new SandboxManagerOptions { ConnectionConfig = BuildConnectionConfig() });

    private ConnectionConfig BuildConnectionConfig()
    {
        if (string.IsNullOrWhiteSpace(_options.OpenSandBox.Address))
        {
            throw new MoAI.Infra.Exceptions.BusinessException("未配置沙箱服务地址（MoAI:OpenSandBox:Address）.") { StatusCode = 500 };
        }

        var uri = Uri.TryCreate(_options.OpenSandBox.Address, UriKind.Absolute, out var parsed)
            ? parsed
            : throw new MoAI.Infra.Exceptions.BusinessException("沙箱服务地址格式不正确.") { StatusCode = 500 };

        var options = new ConnectionConfigOptions
        {
            Domain = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}",
            Protocol = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? ConnectionProtocol.Https : ConnectionProtocol.Http,
            DisableMetrics = true,
            // 创建沙箱可能包含镜像拉取/启动，放宽默认 30s 超时
            RequestTimeoutSeconds = 600,
        };

        if (!string.IsNullOrWhiteSpace(_options.OpenSandBox.ApiKey))
        {
            options.ApiKey = _options.OpenSandBox.ApiKey;
        }

        return new ConnectionConfig(options);
    }

    private int ResolveTimeoutSeconds(SandboxSettings settings)
        => settings.TimeoutSeconds is > 0 ? settings.TimeoutSeconds!.Value : _options.OpenSandBox.TimeoutSeconds;

    private static IReadOnlyDictionary<string, string>? BuildResource(SandboxSettings settings)
    {
        if (settings.Resource == null)
        {
            return null;
        }

        var resource = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(settings.Resource.Cpu))
        {
            resource["cpu"] = settings.Resource.Cpu!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(settings.Resource.Memory))
        {
            resource["memory"] = settings.Resource.Memory!.Trim();
        }

        return resource.Count == 0 ? null : resource;
    }

    private static NetworkPolicy? BuildNetworkPolicy(SandboxSettings settings)
    {
        if (settings.Network == null)
        {
            return null;
        }

        NetworkRuleAction? defaultAction = settings.Network.DefaultAction?.Trim().ToLowerInvariant() switch
        {
            "allow" => NetworkRuleAction.Allow,
            "deny" => NetworkRuleAction.Deny,
            _ => null,
        };

        var egress = settings.Network.Egress
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new NetworkRule
            {
                Target = x.Trim(),
                // 默认 deny 时列出的是放行名单，否则视为拒绝名单
                Action = defaultAction == NetworkRuleAction.Deny ? NetworkRuleAction.Allow : NetworkRuleAction.Deny,
            })
            .ToList();

        if (defaultAction == null && egress.Count == 0)
        {
            return null;
        }

        return new NetworkPolicy { DefaultAction = defaultAction, Egress = egress };
    }

    private Task<SandboxCacheEntry?> GetEntryAsync(Guid sessionId, CancellationToken cancellationToken)
        => _redisDatabase.GetAsync<SandboxCacheEntry>(CacheKey(sessionId));

    private Task WriteEntryAsync(Guid sessionId, SandboxCacheEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
        => _redisDatabase.Database.StringSetAsync(CacheKey(sessionId), entry.ToRedisValue(), ttl);

    private Task RemoveEntryAsync(Guid sessionId, CancellationToken cancellationToken)
        => _redisDatabase.Database.KeyDeleteAsync(CacheKey(sessionId));

    private static string CacheKey(Guid sessionId) => $"appagent:sandbox:{sessionId:N}";

    internal static string MapExecution(Execution execution)
    {
        var stdout = string.Concat(execution.Logs.Stdout.Select(x => x.Text));
        var stderr = string.Concat(execution.Logs.Stderr.Select(x => x.Text));
        var results = execution.Results.Select(x => x.Text).Where(x => x != null).ToList();

        var error = execution.Error == null
            ? null
            : new { name = execution.Error.Name, value = execution.Error.Value, traceback = execution.Error.Traceback };

        return JsonSerializer.Serialize(new
        {
            success = execution.Error == null,
            stdout,
            stderr,
            results,
            exitCode = execution.ExitCode,
            error,
        }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
