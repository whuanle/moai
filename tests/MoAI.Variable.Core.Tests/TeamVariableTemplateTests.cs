using System.Collections.Generic;
using MoAI.Variable.Services;
using Xunit;

namespace MoAI.Variable.Core.Tests;

/// <summary>
/// <see cref="TeamVariableTemplate"/> 插值行为测试.
/// </summary>
public class TeamVariableTemplateTests
{
    private static Dictionary<string, string> Vars(params (string Key, string Value)[] items)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (key, value) in items)
        {
            dict[key] = value;
        }

        return dict;
    }

    [Fact]
    public void Format_BasicReplacement()
    {
        var vars = Vars(("token", "abc123"));
        Assert.Equal("Bearer abc123", TeamVariableTemplate.Format("Bearer {token}", vars));
    }

    [Fact]
    public void Format_MultipleKeys()
    {
        var vars = Vars(("appId", "a1"), ("secret", "s2"));
        Assert.Equal("a1:s2/a1", TeamVariableTemplate.Format("{appId}:{secret}/{appId}", vars));
    }

    [Fact]
    public void Format_MissingKeyKeepsOriginal()
    {
        var vars = Vars(("token", "abc123"));
        Assert.Equal("Bearer {nope}", TeamVariableTemplate.Format("Bearer {nope}", vars));
    }

    [Fact]
    public void Format_EmptyDictionaryKeepsAll()
    {
        Assert.Equal("Bearer {token}", TeamVariableTemplate.Format("Bearer {token}", new Dictionary<string, string>()));
    }

    [Fact]
    public void Format_JsonLiteralBracesUntouched()
    {
        var vars = Vars(("token", "abc123"));
        Assert.Equal("{\"a\":\"abc123\",\"b\":1}", TeamVariableTemplate.Format("{\"a\":\"{token}\",\"b\":1}", vars));
    }

    [Fact]
    public void Format_NumericPlaceholderKeepsOriginal()
    {
        var vars = Vars(("token", "abc123"));
        Assert.Equal("v={0}", TeamVariableTemplate.Format("v={0}", vars));
    }

    [Fact]
    public void Format_ValueContainingBracesOutputAsIs()
    {
        var vars = Vars(("token", "a{b}c"));
        Assert.Equal("Bearer a{b}c", TeamVariableTemplate.Format("Bearer {token}", vars));
    }

    [Fact]
    public void Format_EmptyTemplate()
    {
        var vars = Vars(("token", "abc123"));
        Assert.Equal(string.Empty, TeamVariableTemplate.Format(string.Empty, vars));
    }

    [Fact]
    public void Format_CaseSensitiveKeys()
    {
        var vars = Vars(("Token", "upper"));
        Assert.Equal("{token} upper", TeamVariableTemplate.Format("{token} {Token}", vars));
    }

    [Fact]
    public void Format_ChineseText()
    {
        var vars = Vars(("key", "值"));
        Assert.Equal("密钥=值，超时={timeout}", TeamVariableTemplate.Format("密钥={key}，超时={timeout}", vars));
    }
}
