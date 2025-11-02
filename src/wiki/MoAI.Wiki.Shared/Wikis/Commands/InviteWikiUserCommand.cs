using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 邀请或移除知识库协作成员，可以管理知识库文档等.
/// </summary>
public class InviteWikiUserCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 如果是则邀请，否则是移除.
    /// </summary>
    public bool IsInvite { get; init; } = true;

    /// <summary>
    /// 用户名称.
    /// </summary>
    public IReadOnlyCollection<string> UserNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 用户id.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();
}
