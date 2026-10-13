using System;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// IMAP 搜索条件构造测试（AND 组合语义由 IMAP 桩 E2E 覆盖，这里只覆盖校验分支）.
/// </summary>
public class ImapSearchQueryBuilderTests
{
    [Fact]
    public void Build_NoCriteriaNoRequire_ReturnsQuery()
    {
        var query = ImapSearchQueryBuilder.Build(null, null, null, false, requireAtLeastOne: false);

        Assert.NotNull(query);
    }

    [Fact]
    public void Build_NoCriteriaWithRequire_Throws()
    {
        var ex = Assert.Throws<BusinessException>(() => ImapSearchQueryBuilder.Build(" ", "", null, false, requireAtLeastOne: true));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("至少需要一个过滤条件", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_FromOnly_Passes()
    {
        Assert.NotNull(ImapSearchQueryBuilder.Build("alice", null, null, false, requireAtLeastOne: true));
    }

    [Fact]
    public void Build_SubjectAndDateAndUnread_Passes()
    {
        Assert.NotNull(ImapSearchQueryBuilder.Build(null, "报表", "2026-01-06", true, requireAtLeastOne: true));
    }

    [Fact]
    public void Build_UnreadOnly_Passes()
    {
        Assert.NotNull(ImapSearchQueryBuilder.Build(null, null, null, true, requireAtLeastOne: true));
    }

    [Fact]
    public void Build_ValidSinceDate_QueryHoldsUtcMidnightOfInput()
    {
        var query = ImapSearchQueryBuilder.Build(null, null, "2026-01-06", false, requireAtLeastOne: true);

        var dateQuery = Assert.IsType<MailKit.Search.DateSearchQuery>(query);
        Assert.Equal(new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc), dateQuery.Date);
    }

    [Theory]
    [InlineData("2026/01/06")]
    [InlineData("01-06-2026")]
    [InlineData("not-a-date")]
    public void Build_InvalidDate_Throws(string date)
    {
        var ex = Assert.Throws<BusinessException>(() => ImapSearchQueryBuilder.Build(null, null, date, false, requireAtLeastOne: true));

        Assert.Contains("yyyy-MM-dd", ex.Message, StringComparison.Ordinal);
    }
}
