using System;
using MimeKit;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 邮件正文提取测试.
/// </summary>
public class EmailBodyExtractorTests
{
    [Fact]
    public void GetTextBody_PlainPreferred()
    {
        var message = new MimeMessage();
        var builder = new BodyBuilder
        {
            TextBody = "纯文本正文",
            HtmlBody = "<p>HTML 正文</p>",
        };
        message.Body = builder.ToMessageBody();

        Assert.Equal("纯文本正文", EmailBodyExtractor.GetTextBody(message));
    }

    [Fact]
    public void GetTextBody_HtmlOnly_StrippedToText()
    {
        var message = new MimeMessage();
        var builder = new BodyBuilder
        {
            HtmlBody = "<html><head><style>p{color:red}</style></head><body><!-- 注释 --><p>季度</p>  <b>GMV</b> 128.6</body></html>",
        };
        message.Body = builder.ToMessageBody();

        var text = EmailBodyExtractor.GetTextBody(message);

        Assert.Contains("季度", text, StringComparison.Ordinal);
        Assert.Contains("GMV 128.6", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<", text, StringComparison.Ordinal);
        Assert.DoesNotContain("color:red", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTextBody_NoBody_Empty()
    {
        var message = new MimeMessage();
        message.Body = new TextPart("plain");

        Assert.Equal(string.Empty, EmailBodyExtractor.GetTextBody(message));
    }

    [Fact]
    public void StripHtml_DecodesEntitiesAndCollapsesWhitespace()
    {
        var text = EmailBodyExtractor.StripHtml("<p>A &amp; B</p>\n<p>  C  </p>");

        Assert.Equal("A & B C", text);
    }

    [Fact]
    public void Truncate_WithinLimit_Untouched()
    {
        Assert.Equal("abc", EmailBodyExtractor.Truncate("abc", 100));
    }

    [Fact]
    public void Truncate_OverLimit_MarkedWithOriginalLength()
    {
        var text = EmailBodyExtractor.Truncate(new string('x', 50), 10);

        Assert.StartsWith(new string('x', 10), text, StringComparison.Ordinal);
        Assert.Contains("50", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Truncate_NonPositiveLimit_NoTruncation(int maxChars)
    {
        Assert.Equal("abcdef", EmailBodyExtractor.Truncate("abcdef", maxChars));
    }
}
