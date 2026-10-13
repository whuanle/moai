using System;
using System.Globalization;
using MailKit.Search;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// IMAP 搜索条件构造工具：把请求里的过滤字段组合成 MailKit 的 <see cref="SearchQuery"/>（AND 语义）.
/// </summary>
public static class ImapSearchQueryBuilder
{
    /// <summary>
    /// 构造搜索条件；search 模式下至少需要一个条件，日期只接受 yyyy-MM-dd.
    /// </summary>
    /// <param name="fromContains">发件人包含关键字（可空）.</param>
    /// <param name="subjectContains">主题包含关键字（可空）.</param>
    /// <param name="sinceIsoDate">起始日期 yyyy-MM-dd（可空，含当天）.</param>
    /// <param name="unreadOnly">仅未读.</param>
    /// <param name="requireAtLeastOne">是否要求至少一个条件（search 模式为真，避免与 recent 语义重复）.</param>
    /// <returns>组合后的搜索条件.</returns>
    public static SearchQuery Build(string? fromContains, string? subjectContains, string? sinceIsoDate, bool unreadOnly, bool requireAtLeastOne)
    {
        SearchQuery? query = null;

        Add(ref query, Contains(fromContains, q => SearchQuery.FromContains(q), "发件人 FromContains"));
        Add(ref query, Contains(subjectContains, q => SearchQuery.SubjectContains(q), "主题 SubjectContains"));

        var date = (sinceIsoDate ?? string.Empty).Trim();
        if (date.Length > 0)
        {
            if (!DateTimeOffset.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var since))
            {
                throw new BusinessException(400, "SinceDate 只接受 yyyy-MM-dd 格式，例如 2026-01-01");
            }

            // IMAP 的 SINCE 语义为「含当天」，不需要再前移一天；MailKit 渲染 SINCE 日期取 UTC 部分，
            // 因此把 yyyy-MM-dd 固定为 UTC 零点下发，保证服务端看到的日期与用户输入一致.
            Add(ref query, SearchQuery.DeliveredAfter(since.UtcDateTime.Date));
        }

        if (unreadOnly)
        {
            Add(ref query, SearchQuery.NotSeen);
        }

        if (query == null && requireAtLeastOne)
        {
            throw new BusinessException(400, "search 模式至少需要一个过滤条件：FromContains / SubjectContains / SinceDate / UnreadOnly");
        }

        return query ?? SearchQuery.All;
    }

    /// <summary>
    /// 非空关键字转包含条件.
    /// </summary>
    private static SearchQuery? Contains(string? keyword, Func<string, SearchQuery> factory, string fieldName)
    {
        var value = (keyword ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return factory(value);
    }

    private static void Add(ref SearchQuery? query, SearchQuery? next)
    {
        if (next == null)
        {
            return;
        }

        query = query == null ? next : query.And(next);
    }
}
