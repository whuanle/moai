using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// IMAP 邮箱读取（动态插件）：用实例配置里的 IMAP 服务器与账号读取邮箱，支持最近邮件、条件搜索、读取单封正文、标记已读/未读与文件夹列表.
/// </summary>
/// <remarks>
/// 协议栈为 MailKit（<see cref="ImapClient"/>）；列表模式只取信封摘要（不含正文，避免把整箱邮件拉进上下文），
/// detail 模式才取单封完整内容并按配置截断正文。默认仅做只读操作，唯一的写操作是 mark_read 标记已读/未读。
/// </remarks>
[AiPlugin(key: "imap_email_query", Name = "IMAP 邮箱读取", Description = "通过 IMAP 协议读取邮箱：查看文件夹、最近邮件、按发件人/主题/日期/未读条件搜索、读取单封正文与附件列表、标记已读或未读")]
public class ImapEmailQueryPlugin : IDynamicPluginRuntime<ImapEmailQueryRequest, ImapEmailQueryResponse, ImapEmailQueryConfig>
{
    /// <summary>
    /// 列表条数上限.
    /// </summary>
    private const int MaxCount = 50;

    /// <summary>
    /// 正文截断上限.
    /// </summary>
    private const int MaxBodyCharsLimit = 20_000;

    private string _host = string.Empty;
    private int _port;
    private SecureSocketOptions _security = SecureSocketOptions.SslOnConnect;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _folder = "INBOX";
    private int _timeoutSeconds;
    private int _maxBodyChars;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "recent",          // 操作模式：recent / search / detail / mark_read / folders（默认 recent）
              "Count": 10,               // recent/search：最多返回条数（默认 10）
              "Folder": "",              // 目标文件夹（可选，留空取配置，如 INBOX、Archive）
              "FromContains": "",        // search：发件人包含关键字（可空）
              "SubjectContains": "报表",   // search：主题包含关键字（可空）
              "SinceDate": "",           // search：起始日期 yyyy-MM-dd（可空）
              "UnreadOnly": false,       // search：仅未读（默认 false）
              "Uid": 0,                  // detail/mark_read：目标邮件 UID（必填）
              "Unread": false            // mark_read：false=标记已读（默认），true=标记未读
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "Host": "imap.exmail.qq.com",  // IMAP 服务器地址（必填）
              "Port": 993,                   // 端口（默认 993）
              "Security": "ssl",             // 加密模式：ssl / starttls / none（默认 ssl）
              "Username": "bot@example.com", // IMAP 登录账号（必填）
              "Password": "你的授权码",         // 登录密码或授权码（必填）
              "Folder": "INBOX",             // 默认文件夹（默认收件箱）
              "TimeoutSeconds": 15,          // 超时秒数（默认 15）
              "MaxBodyChars": 4000           // 正文最大字符数（默认 4000，0 不截断）
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(ImapEmailQueryConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            return Task.FromResult<string?>("Host 不能为空，请填写 IMAP 服务器地址");
        }

        if (config.Port < 1 || config.Port > 65535)
        {
            return Task.FromResult<string?>("Port 必须在 1-65535 之间");
        }

        if (string.IsNullOrWhiteSpace(config.Username))
        {
            return Task.FromResult<string?>("Username 不能为空，请填写 IMAP 登录账号");
        }

        if (string.IsNullOrWhiteSpace(config.Password))
        {
            return Task.FromResult<string?>("Password 不能为空，请填写 IMAP 登录密码或授权码");
        }

        SecureSocketOptions security;
        try
        {
            security = MailSecurityParser.Parse(config.Security);
        }
        catch (BusinessException ex)
        {
            return Task.FromResult<string?>(ex.Message);
        }

        _host = config.Host.Trim();
        _port = config.Port;
        _security = security;
        _username = config.Username.Trim();
        _password = config.Password;
        _folder = string.IsNullOrWhiteSpace(config.Folder) ? "INBOX" : config.Folder.Trim();
        _timeoutSeconds = Math.Clamp(config.TimeoutSeconds, 1, 120);
        _maxBodyChars = Math.Clamp(config.MaxBodyChars, 0, MaxBodyCharsLimit);
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<ImapEmailQueryResponse> RunAsync(ImapEmailQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = (request.Mode ?? "recent").Trim().ToLowerInvariant();
        if (mode is not ("recent" or "search" or "detail" or "mark_read" or "folders"))
        {
            throw new BusinessException(400, $"未知操作模式 {mode}，可用值：recent / search / detail / mark_read / folders");
        }

        // 全部用户输入校验先于网络连接，避免非法参数消耗一次 IMAP 会话.
        SearchQuery? searchQuery = null;
        if (mode == "search")
        {
            searchQuery = ImapSearchQueryBuilder.Build(request.FromContains, request.SubjectContains, request.SinceDate, request.UnreadOnly, requireAtLeastOne: true);
        }
        else if (mode == "detail" || mode == "mark_read")
        {
            ValidateUid(request);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        using var client = new ImapClient
        {
            Timeout = _timeoutSeconds * 1000,
        };
        try
        {
            await client.ConnectAsync(_host, _port, _security, timeoutCts.Token).ConfigureAwait(false);
            await client.AuthenticateAsync(_username, _password, timeoutCts.Token).ConfigureAwait(false);

            return mode switch
            {
                "recent" => await ListAsync(client, request, SearchQuery.All, timeoutCts.Token).ConfigureAwait(false),
                "search" => await ListAsync(client, request, searchQuery!, timeoutCts.Token).ConfigureAwait(false),
                "detail" => await DetailAsync(client, request, timeoutCts.Token).ConfigureAwait(false),
                "mark_read" => await MarkReadAsync(client, request, timeoutCts.Token).ConfigureAwait(false),
                _ => await FoldersAsync(client, timeoutCts.Token).ConfigureAwait(false),
            };
        }
        catch (AuthenticationException)
        {
            throw new BusinessException(401, "IMAP 认证失败：账号或密码/授权码错误（部分服务商要求使用授权码并开启 IMAP 服务）");
        }
        catch (FolderNotFoundException ex)
        {
            throw new BusinessException(400, $"邮箱文件夹不存在：{ex.FolderName ?? _folder}，可先用 folders 模式查看可用文件夹");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(504, $"IMAP 连接或命令超时（TimeoutSeconds={_timeoutSeconds}），请检查服务器地址与端口");
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException(502, $"IMAP 读取失败：{FirstLine(ex.Message)}");
        }
    }

    /// <summary>
    /// recent/search 模式：打开文件夹取摘要列表（信封级，不含正文）.
    /// </summary>
    private async Task<ImapEmailQueryResponse> ListAsync(ImapClient client, ImapEmailQueryRequest request, SearchQuery query, CancellationToken cancellationToken)
    {
        var folderName = ResolveFolderName(request);
        var folder = await OpenAsync(client, folderName, FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

        var matched = await folder.SearchAsync(query, cancellationToken).ConfigureAwait(false);

        var count = Math.Clamp(request.Count <= 0 ? 10 : request.Count, 1, MaxCount);
        // SEARCH 返回升序 UID，取末尾 N 条即最新邮件，再倒序输出（新→旧）.
        var take = matched.Count > count ? matched.TakeLast(count).ToArray() : matched.ToArray();
        var summaries = take.Length == 0
            ? new List<ImapEmailMessageSummary>()
            : await FetchSummariesAsync(folder, take, cancellationToken).ConfigureAwait(false);

        return new ImapEmailQueryResponse
        {
            ResultType = (request.Mode ?? string.Empty).Trim().ToLowerInvariant(),
            Folder = folderName,
            TotalCount = matched.Count,
            Messages = summaries,
            Truncated = matched.Count > take.Length,
        };
    }

    /// <summary>
    /// 取一组邮件的信封摘要.
    /// </summary>
    private static async Task<List<ImapEmailMessageSummary>> FetchSummariesAsync(IMailFolder folder, UniqueId[] uids, CancellationToken cancellationToken)
    {
        var items = MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.Size | MessageSummaryItems.InternalDate | MessageSummaryItems.UniqueId;
        var fetched = await folder.FetchAsync(uids, items, cancellationToken).ConfigureAwait(false);

        var result = new List<ImapEmailMessageSummary>();
        foreach (var summary in fetched.OrderByDescending(x => x.UniqueId.Id))
        {
            var mailbox = summary.Envelope?.From?.Mailboxes.FirstOrDefault();
            result.Add(new ImapEmailMessageSummary
            {
                Uid = summary.UniqueId.Id,
                FromName = mailbox?.Name ?? string.Empty,
                FromAddress = mailbox?.Address ?? string.Empty,
                Subject = summary.Envelope?.Subject ?? string.Empty,
                DateIso = FormatDate(summary.Envelope?.Date ?? summary.InternalDate),
                Seen = HasFlag(summary, MessageFlags.Seen),
                SizeBytes = summary.Size ?? 0,
            });
        }

        return result;
    }

    /// <summary>
    /// detail 模式：取单封完整内容（正文按 MaxBodyChars 截断，附件只回元信息）.
    /// </summary>
    private async Task<ImapEmailQueryResponse> DetailAsync(ImapClient client, ImapEmailQueryRequest request, CancellationToken cancellationToken)
    {
        var uid = ValidateUid(request);
        var folderName = ResolveFolderName(request);
        var folder = await OpenAsync(client, folderName, FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

        var message = await folder.GetMessageAsync(new UniqueId((uint)uid), cancellationToken).ConfigureAwait(false);

        // GetMessage 只取正文，已读状态需要单独取一次 FLAGS.
        var flags = await folder.FetchAsync(new[] { new UniqueId((uint)uid) }, MessageSummaryItems.Flags, cancellationToken).ConfigureAwait(false);
        var seen = flags.Count > 0 && HasFlag(flags[0], MessageFlags.Seen);

        var detail = new ImapEmailMessageDetail
        {
            Uid = uid,
            MessageId = message.MessageId ?? string.Empty,
            FromName = message.From.Mailboxes.FirstOrDefault()?.Name ?? string.Empty,
            FromAddress = message.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty,
            To = AddressesOf(message.To),
            Cc = AddressesOf(message.Cc),
            Subject = message.Subject ?? string.Empty,
            DateIso = FormatDate(message.Date),
            Seen = seen,
            BodyText = EmailBodyExtractor.Truncate(EmailBodyExtractor.GetTextBody(message), _maxBodyChars),
            Attachments = AttachmentsOf(message),
        };

        return new ImapEmailQueryResponse
        {
            ResultType = "detail",
            Folder = folderName,
            Message = detail,
        };
    }

    /// <summary>
    /// mark_read 模式：标记已读/未读（唯一的写操作，需读写方式打开）.
    /// </summary>
    private async Task<ImapEmailQueryResponse> MarkReadAsync(ImapClient client, ImapEmailQueryRequest request, CancellationToken cancellationToken)
    {
        var uid = ValidateUid(request);
        var folderName = ResolveFolderName(request);
        var folder = await OpenAsync(client, folderName, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

        var uniqueId = new UniqueId((uint)uid);
        if (request.Unread)
        {
            await folder.RemoveFlagsAsync(uniqueId, MessageFlags.Seen, silent: true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await folder.AddFlagsAsync(uniqueId, MessageFlags.Seen, silent: true, cancellationToken).ConfigureAwait(false);
        }

        return new ImapEmailQueryResponse
        {
            ResultType = "mark_read",
            Folder = folderName,
            UpdatedUid = uid,
            Unread = request.Unread,
        };
    }

    /// <summary>
    /// folders 模式：列出个人命名空间下的全部文件夹.
    /// </summary>
    private async Task<ImapEmailQueryResponse> FoldersAsync(ImapClient client, CancellationToken cancellationToken)
    {
        // 未协商 NAMESPACE 扩展时 PersonalNamespaces 为空，回退到空前缀根目录.
        var ns = client.PersonalNamespaces.Count > 0 ? client.PersonalNamespaces[0] : new FolderNamespace('/', string.Empty);
        var folders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false, cancellationToken).ConfigureAwait(false);

        return new ImapEmailQueryResponse
        {
            ResultType = "folders",
            Folder = _folder,
            Folders = folders.Select(x => new ImapFolderInfo
            {
                FullName = x.FullName,
                Name = x.Name,
            }).ToList(),
        };
    }

    /// <summary>
    /// 解析目标文件夹名：请求优先，回退实例配置.
    /// </summary>
    private string ResolveFolderName(ImapEmailQueryRequest request)
    {
        var name = (request.Folder ?? string.Empty).Trim();
        return name.Length > 0 ? name : _folder;
    }

    /// <summary>
    /// 打开文件夹（按需只读/读写）.
    /// </summary>
    private static async Task<IMailFolder> OpenAsync(ImapClient client, string folderName, FolderAccess access, CancellationToken cancellationToken)
    {
        var folder = await client.GetFolderAsync(folderName, cancellationToken).ConfigureAwait(false);
        await folder.OpenAsync(access, cancellationToken).ConfigureAwait(false);
        return folder;
    }

    /// <summary>
    /// 校验 detail/mark_read 模式的 UID 参数.
    /// </summary>
    private static long ValidateUid(ImapEmailQueryRequest request)
    {
        if (request.Uid <= 0)
        {
            throw new BusinessException(400, "Uid 不能为空，请从 recent/search 结果里取目标邮件的 Uid");
        }

        if (request.Uid > uint.MaxValue)
        {
            throw new BusinessException(400, $"Uid 超出范围（最大 {uint.MaxValue}）：{request.Uid}");
        }

        return request.Uid;
    }

    /// <summary>
    /// 读取摘要里的邮件旗标（FlagsSummary 内部值可能为空）.
    /// </summary>
    private static bool HasFlag(IMessageSummary summary, MessageFlags flag)
    {
        return summary.Flags.HasValue && summary.Flags.Value.HasFlag(flag);
    }

    /// <summary>
    /// 地址列表转「Name &lt;addr&gt;」字符串列表.
    /// </summary>
    private static List<string> AddressesOf(InternetAddressList list)
    {
        return list.Mailboxes
            .Select(x => string.IsNullOrWhiteSpace(x.Name) ? x.Address : $"{x.Name} <{x.Address}>")
            .ToList();
    }

    /// <summary>
    /// 提取附件元信息（文件名/类型/大小），不回传附件内容.
    /// </summary>
    private static List<ImapEmailAttachment> AttachmentsOf(MimeMessage message)
    {
        var result = new List<ImapEmailAttachment>();
        foreach (var attachment in message.Attachments)
        {
            string fileName;
            long size;
            var mimePart = attachment as MimePart;
            if (mimePart != null)
            {
                fileName = mimePart.FileName ?? mimePart.ContentType.Name ?? string.Empty;
                size = 0;
                try
                {
                    using var stream = new System.IO.MemoryStream();
                    mimePart.Content?.DecodeTo(stream, CancellationToken.None);
                    size = stream.Length;
                }
                catch (Exception)
                {
                    // 附件内容解码失败不影响元信息返回.
                }

                result.Add(new ImapEmailAttachment
                {
                    FileName = fileName,
                    ContentType = mimePart.ContentType.MimeType,
                    SizeBytes = size,
                });
            }
            else if (attachment is MessagePart)
            {
                result.Add(new ImapEmailAttachment
                {
                    FileName = "embedded-message.eml",
                    ContentType = "message/rfc822",
                    SizeBytes = 0,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 时间转 ISO 8601；空值返回空串.
    /// </summary>
    private static string FormatDate(DateTimeOffset? date)
    {
        return date?.ToString("yyyy-MM-ddTHH:mm:sszzz") ?? string.Empty;
    }

    private static string FirstLine(string message)
    {
        var line = (message ?? string.Empty).Split('\n').FirstOrDefault()?.Trim('\r') ?? string.Empty;
        return string.IsNullOrWhiteSpace(line) ? "未知错误" : line;
    }
}
