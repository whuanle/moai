using System;
using System.Collections.Generic;
using System.Linq;
using MoAI.TeamAgent;
using MoAI.TeamAgent.Models;
using Xunit;

namespace MoAI.TeamAgent.Tests;

/// <summary>
/// <c>team_agent</c> JSON 文本列读写辅助测试.
/// </summary>
public class TeamAgentJsonTests
{
    [Fact]
    public void ParseGuidList_Roundtrip()
    {
        var ids = new List<Guid> { Guid.CreateVersion7(), Guid.CreateVersion7() };
        var json = TeamAgentJson.SerializePluginIds(ids);

        var parsed = TeamAgentJson.ParseGuidList(json);

        Assert.Equal(ids, parsed);
    }

    [Fact]
    public void ParseGuidList_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.Empty(TeamAgentJson.ParseGuidList(null));
        Assert.Empty(TeamAgentJson.ParseGuidList(string.Empty));
        Assert.Empty(TeamAgentJson.ParseGuidList("not-json"));
        Assert.Empty(TeamAgentJson.ParseGuidList("[\"not-a-guid\"]"));
        Assert.Empty(TeamAgentJson.ParseGuidList("[\"00000000-0000-0000-0000-000000000000\"]"));
    }

    [Fact]
    public void ParseLongList_Roundtrip()
    {
        var json = TeamAgentJson.SerializeWikiIds(new List<long> { 1, 2, 3 });

        var parsed = TeamAgentJson.ParseLongList(json);

        Assert.Equal(new List<long> { 1, 2, 3 }, parsed);
    }

    [Fact]
    public void ParseLongList_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.Empty(TeamAgentJson.ParseLongList(null));
        Assert.Empty(TeamAgentJson.ParseLongList(string.Empty));
        Assert.Empty(TeamAgentJson.ParseLongList("not-json"));
    }

    [Fact]
    public void ParseSubs_Roundtrip_PreservesAllFields()
    {
        var subs = new List<TeamAgentSubItem>
        {
            new TeamAgentSubItem
            {
                Key = "researcher",
                Name = "资料调研员",
                Description = "负责资料检索与整理",
                Prompt = "你是检索专员",
                PluginIds = new List<Guid> { Guid.CreateVersion7() },
                WikiIds = new List<long> { 11, 22 },
                GraphIds = new List<long> { 33 },
            },
            new TeamAgentSubItem
            {
                Key = "writer",
                Name = "写作员",
                Description = "负责成稿",
                Prompt = "你是写作专员",
                PluginIds = new List<Guid>(),
                WikiIds = new List<long>(),
                GraphIds = new List<long>(),
            },
        };

        var json = TeamAgentJson.SerializeSubs(subs);
        var parsed = TeamAgentJson.ParseSubs(json);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("researcher", parsed[0].Key);
        Assert.Equal("资料调研员", parsed[0].Name);
        Assert.Equal(subs[0].Description, parsed[0].Description);
        Assert.Equal(subs[0].Prompt, parsed[0].Prompt);
        Assert.Equal(subs[0].PluginIds, parsed[0].PluginIds);
        Assert.Equal(subs[0].WikiIds, parsed[0].WikiIds);
        Assert.Equal(subs[0].GraphIds, parsed[0].GraphIds);
    }

    [Fact]
    public void ParseSubs_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.Empty(TeamAgentJson.ParseSubs(null));
        Assert.Empty(TeamAgentJson.ParseSubs(string.Empty));
        Assert.Empty(TeamAgentJson.ParseSubs("{}"));
    }

    [Fact]
    public void SerializePluginIds_AlwaysNonEmptyJsonArray()
    {
        Assert.Equal("[]", TeamAgentJson.SerializePluginIds(new List<Guid>()));
        Assert.Equal("[]", TeamAgentJson.SerializeWikiIds(new List<long>()));
        Assert.Equal("[]", TeamAgentJson.SerializeSubs(new List<TeamAgentSubItem>()));
    }
}

/// <summary>
/// 子智能体标识唯一性校验测试.
/// </summary>
public class TeamAgentDependencyGuardSubNameTests
{
    [Fact]
    public void EnsureSubKeysUnique_UniqueKeys_Passes()
    {
        var subs = new List<TeamAgentSubItem>
        {
            new TeamAgentSubItem { Key = "researcher", Name = "资料调研员", Description = "d", Prompt = "p" },
            new TeamAgentSubItem { Key = "writer", Name = "写作员", Description = "d", Prompt = "p" },
        };

        MoAI.TeamAgent.Services.TeamAgentDependencyGuard.EnsureSubKeysUnique(subs);
    }

    [Fact]
    public void EnsureSubKeysUnique_DuplicatedKeys_Throws400()
    {
        var subs = new List<TeamAgentSubItem>
        {
            new TeamAgentSubItem { Key = "researcher", Name = "调研员A", Description = "d", Prompt = "p" },
            new TeamAgentSubItem { Key = "researcher", Name = "调研员B", Description = "d2", Prompt = "p2" },
        };

        var exception = Assert.Throws<MoAI.Infra.Exceptions.BusinessException>(
            () => MoAI.TeamAgent.Services.TeamAgentDependencyGuard.EnsureSubKeysUnique(subs));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("researcher", exception.Message);
    }

    [Fact]
    public void EnsureSubKeysUnique_Empty_Passes()
    {
        MoAI.TeamAgent.Services.TeamAgentDependencyGuard.EnsureSubKeysUnique(new List<TeamAgentSubItem>());
    }
}

/// <summary>
/// 内置智能体定义合法性测试：种入前保证数据本身可通过全部创建校验.
/// </summary>
public class BuiltinTeamAgentsTests
{
    [Fact]
    public void All_ContainsPlanAndBuild_WithUniqueKeys()
    {
        var all = MoAI.TeamAgent.Services.BuiltinTeamAgents.All();

        Assert.Equal(2, all.Count);
        Assert.Distinct(all.Select(x => x.Key));
        Assert.Contains(all, x => x.Key == "plan");
        Assert.Contains(all, x => x.Key == "build");
    }

    [Fact]
    public void All_DefinitionsPassCreationRules()
    {
        var keyRegex = new System.Text.RegularExpressions.Regex("^[a-z][a-z0-9_]{0,29}$");

        foreach (var definition in MoAI.TeamAgent.Services.BuiltinTeamAgents.All())
        {
            Assert.Matches(keyRegex, definition.Key);
            Assert.False(string.IsNullOrWhiteSpace(definition.Name));
            Assert.True(definition.Name.Length <= 50, $"{definition.Name} 名称超长");
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.True(definition.Description.Length <= 500, $"{definition.Name} 描述超长");
            Assert.False(string.IsNullOrWhiteSpace(definition.MainPrompt));
            Assert.True(definition.MainPrompt.Length <= 50000, $"{definition.Name} 主提示词超长");
            Assert.NotEmpty(definition.Subs);
            Assert.True(definition.Subs.Count <= 20, $"{definition.Name} 子智能体超限");

            MoAI.TeamAgent.Services.TeamAgentDependencyGuard.EnsureSubKeysUnique(definition.Subs);
            foreach (var sub in definition.Subs)
            {
                Assert.Matches(keyRegex, sub.Key);
                Assert.False(string.IsNullOrWhiteSpace(sub.Name));
                Assert.True(sub.Name.Length <= 50, $"{definition.Name}/{sub.Key} 子名称超长");
                Assert.False(string.IsNullOrWhiteSpace(sub.Description));
                Assert.True(sub.Description.Length <= 500, $"{definition.Name}/{sub.Key} 子描述超长");
                Assert.False(string.IsNullOrWhiteSpace(sub.Prompt));
                Assert.True(sub.Prompt.Length <= 30000, $"{definition.Name}/{sub.Key} 子提示词超长");
            }
        }
    }
}
