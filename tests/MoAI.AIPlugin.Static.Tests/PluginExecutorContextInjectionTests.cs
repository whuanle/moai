using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.AIPlugin.Static.Models;
using MoAI.Infra.Models;
using Xunit;

namespace MoAI.AIPlugin.Static.Tests;

/// <summary>
/// 插件执行引擎的上下文注入测试：执行入口传入的 PluginRunContext 应在插件实例化前写入作用域内的访问器.
/// </summary>
public class PluginExecutorContextInjectionTests
{
    private sealed class FakeRunContextAccessor : IPluginRunContextAccessor
    {
        public PluginRunContext? Context { get; private set; }

        public PluginRunUser? UserResult { get; set; }

        public void Set(PluginRunContext? context)
        {
            Context = context;
        }

        public Task<PluginRunUser?> GetUserAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Context?.IsAuthenticated == true
                ? UserResult ?? new PluginRunUser { Id = Context.UserId, UserName = "db-user" }
                : null);
        }
    }

    private sealed class SinglePluginRegistry : IPluginRegistry
    {
        private readonly PluginInfo _plugin;

        public SinglePluginRegistry(PluginInfo plugin)
        {
            _plugin = plugin;
        }

        public IReadOnlyList<PluginInfo> GetAll() => [_plugin];

        public PluginInfo? Get(string key) => string.Equals(key, _plugin.Key, StringComparison.OrdinalIgnoreCase) ? _plugin : null;

        public void RegisterAssembly(Assembly assembly)
        {
        }
    }

    [AiPlugin(key: "test_context_run", Name = "上下文读取", Description = "测试用")]
    private sealed class ContextRunPlugin : IStaticPluginRuntime<CurrentUserRequest, CurrentUserResponse>
    {
        private readonly IPluginRunContextAccessor _accessor;

        public ContextRunPlugin(IPluginRunContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public static string GetParamsExampleValue() => "{}";

        public async Task<CurrentUserResponse> RunAsync(CurrentUserRequest request, CancellationToken cancellationToken)
        {
            CapturedAccessor = _accessor;
            var user = await _accessor.GetUserAsync(cancellationToken);
            var context = _accessor.Context;
            return new CurrentUserResponse
            {
                IsContextInjected = context != null,
                IsAuthenticated = context?.IsAuthenticated == true,
                UserId = context?.UserId ?? 0,
                UserName = user?.UserName ?? string.Empty,
                UserType = context?.UserType.ToString().ToLowerInvariant() ?? string.Empty,
                Source = context?.Source.ToString().ToLowerInvariant() ?? string.Empty,
            };
        }

        public static IPluginRunContextAccessor? CapturedAccessor { get; set; }
    }

    private static (PluginExecutor Executor, IServiceProvider Provider) CreateExecutor(PluginInfo plugin)
    {
        var services = new ServiceCollection();
        services.AddScoped<IPluginRunContextAccessor, FakeRunContextAccessor>();
        var provider = services.BuildServiceProvider();
        var executor = new PluginExecutor(provider.GetRequiredService<IServiceScopeFactory>(), new SinglePluginRegistry(plugin));
        return (executor, provider);
    }

    private static PluginInfo Info<TPlugin>()
        where TPlugin : class
    {
        return PluginTypeHelper.TryGetPluginInfo(typeof(TPlugin)) ?? throw new InvalidOperationException("测试插件未识别");
    }

    [Fact]
    public async Task ExecuteAsync_InjectsContext_BeforePluginInstantiation()
    {
        var (executor, provider) = CreateExecutor(Info<ContextRunPlugin>());
        using (provider as IDisposable)
        {
            var context = new PluginRunContext
            {
                UserId = 42,
                UserType = UserType.Normal,
                TeamId = 7,
                Source = PluginRunSource.Agent,
            };

            var result = await executor.ExecuteAsync(Info<ContextRunPlugin>(), "{}", null, context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Same(context, ContextRunPlugin.CapturedAccessor?.Context);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithContext_ReturnsUserInfoInDataJson()
    {
        var (executor, provider) = CreateExecutor(Info<ContextRunPlugin>());
        using (provider as IDisposable)
        {
            var result = await executor.ExecuteAsync(
                Info<ContextRunPlugin>(),
                "{}",
                null,
                new PluginRunContext { UserId = 42, UserType = UserType.Normal, Source = PluginRunSource.Admin },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("db-user", result.DataJson, StringComparison.Ordinal);
            Assert.Contains("\"Source\":\"admin\"", result.DataJson, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithoutContext_PluginSeesNullContext()
    {
        var (executor, provider) = CreateExecutor(Info<ContextRunPlugin>());
        using (provider as IDisposable)
        {
            var result = await executor.ExecuteAsync(Info<ContextRunPlugin>(), "{}", null, null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Null(ContextRunPlugin.CapturedAccessor?.Context);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PluginWithoutAccessorDependency_StillRuns()
    {
        var (executor, provider) = CreateExecutor(Info<CurrentTimeLikePlugin>());
        using (provider as IDisposable)
        {
            var result = await executor.ExecuteAsync(
                Info<CurrentTimeLikePlugin>(),
                "{}",
                null,
                new PluginRunContext { UserId = 1, Source = PluginRunSource.Admin },
                CancellationToken.None);

            Assert.True(result.Success);
        }
    }

    [AiPlugin(key: "test_no_context", Name = "无依赖", Description = "测试用")]
    private sealed class CurrentTimeLikePlugin : IStaticPluginRuntime<CurrentUserRequest, CurrentUserResponse>
    {
        public static string GetParamsExampleValue() => "{}";

        public Task<CurrentUserResponse> RunAsync(CurrentUserRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CurrentUserResponse());
        }
    }
}
