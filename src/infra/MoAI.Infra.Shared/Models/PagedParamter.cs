using System.Text.Json.Serialization;

namespace MoAI.Infra.Models;

/// <summary>
/// 分页参数.
/// </summary>
public class PagedParamter : IPagedParamter
{
    private int _pageNo = 1;

    /// <summary>
    /// 页码，从1开始.
    /// </summary>
    public int PageNo
    {
        get
        {
            return _pageNo;
        }

        set
        {
            if (value < 1)
            {
                _pageNo = 1;
            }
            else
            {
                _pageNo = value;
            }
        }
    }

    private int _pageSize = 20;

    /// <summary>
    /// 每页大小.
    /// </summary>
    public int PageSize
    {
        get
        {
            return _pageSize;
        }

        set
        {
            if (value < 1)
            {
                _pageSize = 10;
            }
            else if (value > 1000)
            {
                _pageSize = 1000;
            }
            else
            {
                _pageSize = value;
            }
        }
    }

    /// <summary>
    /// 计算跳过的记录数.
    /// </summary>
    [JsonIgnore]
    public int Skip => (PageNo - 1) * PageSize;

    /// <summary>
    /// 计算获取的记录数.
    /// </summary>
    [JsonIgnore]
    public int Take => PageSize;
}
