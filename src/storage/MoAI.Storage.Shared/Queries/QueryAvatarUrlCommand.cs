using MediatR;

namespace MoAI.Storage.Queries;

public class QueryAvatarUrlCommand : IRequest<IReadOnlyCollection<IAvatarPath>>
{
    /// <summary>
    /// 超时时间.
    /// </summary>
    public TimeSpan ExpiryDuration { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// 头像 ObjectKey 列表.
    /// </summary>
    public IReadOnlyCollection<IAvatarPath> Items { get; init; } = Array.Empty<IAvatarPath>();
}

public interface IAvatarPath
{
    /// <summary>
    /// 头像 url.
    /// </summary>
    public string Avatar { get; set; }

    /// <summary>
    /// 头像 ObjectKey.
    /// </summary>
    public string AvatarKey { get; set; }
}
