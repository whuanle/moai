using System;
using System.Linq;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 邮件收件人解析测试.
/// </summary>
public class EmailRecipientParserTests
{
    [Fact]
    public void Split_MultipleSeparators_TokensTrimmed()
    {
        var tokens = EmailRecipientParser.Split("a@x.com, b@x.com;c@x.com\nd@x.com");

        Assert.Equal(new[] { "a@x.com", "b@x.com", "c@x.com", "d@x.com" }, tokens);
    }

    [Fact]
    public void Split_DuplicatesIgnoredCaseAndOrder()
    {
        var tokens = EmailRecipientParser.Split("A@X.com, a@x.com, b@x.com");

        Assert.Equal(new[] { "A@X.com", "b@x.com" }, tokens);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_EmptyInput_EmptyList(string? raw)
    {
        Assert.Empty(EmailRecipientParser.Split(raw));
    }

    [Fact]
    public void Parse_ValidAddresses_ReturnsList()
    {
        var list = EmailRecipientParser.Parse("a@x.com; b@x.com", "To");

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Parse_InvalidAddress_ThrowsWithToken()
    {
        var ex = Assert.Throws<BusinessException>(() => EmailRecipientParser.Parse("a@x.com, not-an-email", "To"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("not-an-email", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Empty_ThrowsFieldName()
    {
        var ex = Assert.Throws<BusinessException>(() => EmailRecipientParser.Parse(" , ;", "To"));

        Assert.Contains("To", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("User Name <named@example.com>", "named@example.com")]
    public void TryParseAddress_AcceptsPlainAndNamedForms(string raw, string expected)
    {
        Assert.True(EmailRecipientParser.TryParseAddress(raw, out var address));
        Assert.Equal(expected, address);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    [InlineData("@x.com")]
    public void TryParseAddress_RejectsMalformed(string raw)
    {
        Assert.False(EmailRecipientParser.TryParseAddress(raw, out _));
    }

    [Fact]
    public void ParseSingle_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, EmailRecipientParser.ParseSingle(null, "ReplyTo"));
        Assert.Equal(string.Empty, EmailRecipientParser.ParseSingle("   ", "ReplyTo"));
    }

    [Fact]
    public void ParseSingle_NamedForm_TakesAddressOnly()
    {
        var address = EmailRecipientParser.ParseSingle("Reply Desk <reply@example.com>", "ReplyTo");

        Assert.Equal("reply@example.com", address);
    }

    [Fact]
    public void ParseSingle_Invalid_Throws()
    {
        Assert.Throws<BusinessException>(() => EmailRecipientParser.ParseSingle("oops@", "ReplyTo"));
    }

    [Fact]
    public void Parse_NameFormKeptAsToken()
    {
        // 带显示名的地址是合法输入，解析后保留原文（MIME 组装阶段再取纯地址）.
        var list = EmailRecipientParser.Parse("User <user@example.com>", "To");

        Assert.Equal("User <user@example.com>", list.Single());
    }
}
