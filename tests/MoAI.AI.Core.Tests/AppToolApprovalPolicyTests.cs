using MoAI.AI;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// AppToolApprovalPolicy（execution_settings.toolApproval 节）的解析与放行判定：
/// 六类绑定资源白名单（插件/流程应用/Agent 应用/技能为 Guid，知识库/知识图谱为 long）与沙箱开关.
/// </summary>
public class AppToolApprovalPolicyTests
{
    private static readonly Guid PluginA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PluginB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AppA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SkillA = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Parse_FullNode_ReadsAllWhitelistsAndSandboxFlag()
    {
        var json = $$"""
        {
          "sandbox": { "enabled": true },
          "toolApproval": {
            "autoApprovePlugins": ["{{PluginA}}", "{{PluginB}}"],
            "autoApproveWorkflowApps": ["{{AppA}}"],
            "autoApproveAgentApps": ["{{AppA}}"],
            "autoApproveSkills": ["{{SkillA}}"],
            "autoApproveWikis": [1, 2],
            "autoApproveGraphs": [3],
            "sandboxAutoApproved": true
          }
        }
        """;

        var policy = AppToolApprovalPolicy.Parse(json);

        Assert.True(policy.SandboxAutoApproved);
        Assert.Equal([PluginA, PluginB], policy.AutoApprovePlugins);
        Assert.Equal([AppA], policy.AutoApproveWorkflowApps);
        Assert.Equal([AppA], policy.AutoApproveAgentApps);
        Assert.Equal([SkillA], policy.AutoApproveSkills);
        Assert.Equal([1L, 2L], policy.AutoApproveWikis);
        Assert.Equal([3L], policy.AutoApproveGraphs);
    }

    [Fact]
    public void Parse_MissingNode_ReturnsEmpty()
    {
        var policy = AppToolApprovalPolicy.Parse("""{"sandbox":{"enabled":true}}""");

        Assert.Same(AppToolApprovalPolicy.Empty, policy);
        Assert.False(policy.SandboxAutoApproved);
        Assert.Empty(policy.AutoApprovePlugins);
        Assert.Empty(policy.AutoApproveWorkflowApps);
        Assert.Empty(policy.AutoApproveAgentApps);
        Assert.Empty(policy.AutoApproveSkills);
        Assert.Empty(policy.AutoApproveWikis);
        Assert.Empty(policy.AutoApproveGraphs);
    }

    [Fact]
    public void Parse_BlankOrInvalidJson_ReturnsEmpty()
    {
        Assert.Same(AppToolApprovalPolicy.Empty, AppToolApprovalPolicy.Parse(null));
        Assert.Same(AppToolApprovalPolicy.Empty, AppToolApprovalPolicy.Parse("   "));
        Assert.Same(AppToolApprovalPolicy.Empty, AppToolApprovalPolicy.Parse("{not json"));
    }

    [Fact]
    public void Parse_InvalidEntries_SkippedAndDeduplicated()
    {
        var json = $$"""
        {
          "toolApproval": {
            "autoApprovePlugins": ["{{PluginA}}", "{{PluginA}}", "not-a-guid", "00000000-0000-0000-0000-000000000000", 42],
            "autoApproveWorkflowApps": ["{{AppA}}", "{{AppA}}"],
            "autoApproveWikis": [1, 1, 0, -2, "3", 4.5]
          }
        }
        """;

        var policy = AppToolApprovalPolicy.Parse(json);

        Assert.Equal([PluginA], policy.AutoApprovePlugins);
        Assert.Equal([AppA], policy.AutoApproveWorkflowApps);
        // 0/负数/字符串/小数形态一律丢弃，仅保留合法正整数并去重
        Assert.Equal([1L], policy.AutoApproveWikis);
    }

    [Fact]
    public void IsAutoApproved_SandboxFlagMatchesSandboxKindOnly()
    {
        var policy = new AppToolApprovalPolicy { SandboxAutoApproved = true };

        Assert.True(policy.IsAutoApproved("sandbox", null));
        Assert.True(policy.IsAutoApproved("sandbox", PluginA));
        // 沙箱开关只放行沙箱类工具，其余来源类型不受影响
        Assert.False(policy.IsAutoApproved("static", null));
        Assert.False(policy.IsAutoApproved("workflow", PluginB));
    }

    [Fact]
    public void IsAutoApproved_PluginWhitelistMatchesSourceId()
    {
        var policy = new AppToolApprovalPolicy { AutoApprovePlugins = [PluginA] };

        Assert.True(policy.IsAutoApproved("static", PluginA));
        Assert.True(policy.IsAutoApproved("dynamic", PluginA));
        Assert.True(policy.IsAutoApproved("mcp", PluginA));
        Assert.True(policy.IsAutoApproved("openapi", PluginA));
        Assert.False(policy.IsAutoApproved("static", PluginB));
        Assert.False(policy.IsAutoApproved("static", null));
    }

    [Fact]
    public void IsAutoApproved_AppAndSkillWhitelistsMatchTheirOwnKind()
    {
        var policy = new AppToolApprovalPolicy
        {
            AutoApproveWorkflowApps = [AppA],
            AutoApproveAgentApps = [AppA],
            AutoApproveSkills = [SkillA],
        };

        Assert.True(policy.IsAutoApproved("workflow", AppA));
        Assert.True(policy.IsAutoApproved("agentapp", AppA));
        Assert.True(policy.IsAutoApproved("skill", SkillA));
        // 白名单按来源类型隔离：插件白名单为空，同一 id 不放行插件工具
        Assert.False(policy.IsAutoApproved("static", AppA));
        Assert.False(policy.IsAutoApproved("workflow", SkillA));
        Assert.False(policy.IsAutoApproved("agentapp", null));
    }

    [Fact]
    public void IsAutoApproved_WikiAndGraphRequireAllSourcesApproved()
    {
        var policy = new AppToolApprovalPolicy { AutoApproveWikis = [1, 2], AutoApproveGraphs = [3] };

        // 全部绑定资源命中白名单才放行
        Assert.True(policy.IsAutoApproved("wiki", null, [1, 2]));
        Assert.True(policy.IsAutoApproved("graph", null, [3]));
        // 任一未命中即挂起
        Assert.False(policy.IsAutoApproved("wiki", null, [1, 3]));
        Assert.False(policy.IsAutoApproved("wiki", null, [1, 2, 5]));
        // 空列表/无来源不自动放行（无绑定资源时工具本不存在，防御性兜底）
        Assert.False(policy.IsAutoApproved("wiki", null, []));
        Assert.False(policy.IsAutoApproved("wiki", null));
    }

    [Fact]
    public void IsAutoApproved_EmptyPolicy_AutoApprovesNothing()
    {
        Assert.False(AppToolApprovalPolicy.Empty.IsAutoApproved("sandbox", null));
        Assert.False(AppToolApprovalPolicy.Empty.IsAutoApproved("mcp", PluginA));
        Assert.False(AppToolApprovalPolicy.Empty.IsAutoApproved("wiki", null, [1]));
    }

    [Fact]
    public void RequireApprovalKinds_CoverAllBindableResourceTools()
    {
        // 知识库/图谱检索与技能装载已纳入审批范围；内部工具（记忆/子代理）不拦截
        Assert.True(AppToolApprovalContract.KindRequiresApproval("sandbox"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("dynamic"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("static"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("mcp"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("openapi"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("workflow"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("agentapp"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("skill"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("wiki"));
        Assert.True(AppToolApprovalContract.KindRequiresApproval("graph"));
        Assert.False(AppToolApprovalContract.KindRequiresApproval("memory"));
        Assert.False(AppToolApprovalContract.KindRequiresApproval("subagent"));
    }
}
