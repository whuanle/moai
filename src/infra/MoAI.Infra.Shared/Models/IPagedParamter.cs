namespace MoAI.Infra.Models;

/// <summary>
/// 分页属性.
/// </summary>
public interface IPagedParamter
{
    /// <summary>
    /// 第几页.
    /// </summary>
    int PageNo { get; set; }

    /// <summary>
    /// 数量.
    /// </summary>
    int PageSize { get; set; }

    /// <summary>
    /// 偏移量.
    /// </summary>
    int Skip { get; }

    /// <summary>
    /// 选取数量.
    /// </summary>
    int Take { get; }
}