namespace MoAI.AIPlugin.Dynamic.HotBoard;

/// <summary>
/// 热榜渠道：一个平台一张榜单，由 <see cref="HotBoardCatalog"/> 按来源标识构造.
/// </summary>
public interface IHotBoardChannel
{
    /// <summary>
    /// 渠道标识（如 zhihu）.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 拉取榜单条目（按榜单顺序返回，排名由调用方按序号补齐）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回归一化条目列表.</returns>
    Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken);
}
