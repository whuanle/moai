using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MoAI.LoginSafety.Services;

/// <summary>
/// IP 归属地解析结果.
/// </summary>
public class IpLocationInfo
{
    /// <summary>
    /// 国家，未知为空.
    /// </summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>
    /// 省份，未知为空.
    /// </summary>
    public string Province { get; init; } = string.Empty;

    /// <summary>
    /// 城市，未知为空.
    /// </summary>
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// 运营商，未知为空.
    /// </summary>
    public string Isp { get; init; } = string.Empty;
}

/// <summary>
/// IP 归属地解析服务（IPv4，基于 ip2region 离线 xdb 数据）.
/// </summary>
public interface IIpLocationService
{
    /// <summary>
    /// 地理库是否可用（xdb 文件存在且已加载），不可用时解析返回空结果.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 解析 IP 归属地；解析失败或库不可用时返回空结果，不抛异常.
    /// </summary>
    /// <param name="ip">IPv4 地址.</param>
    /// <returns>返回 <see cref="IpLocationInfo"/>.</returns>
    IpLocationInfo Resolve(string ip);
}

/// <summary>
/// ip2region xdb（structure 3.0）读取器：启动时将 xdb 全量载入内存（约 11MB）.
/// <para>xdb 3.0 布局 = 256 字节定长头 + 向量索引区(256*256*8) + 段索引/数据区；
/// 段索引项 14 字节（起始IP4 + 结束IP4 + 数据长度2 + 数据指针4），数据区为归属地原文（国家|省份|城市|ISP|国家码）。</para>
/// <para>xdb 缺失或损坏时自动降级（IsAvailable=false），不影响登录流程.</para>
/// </summary>
public class Ip2RegionService : IIpLocationService
{
    private const int HeaderLength = 256;
    private const int VectorIndexLength = 256 * 256 * 8;
    private const int SegmentIndexSize = 14;

    private static readonly string[] CandidatePaths =
    {
        Path.Combine(AppContext.BaseDirectory, "configs", "ip2region.xdb"),
        Path.Combine(AppContext.BaseDirectory, "ip2region.xdb"),
        Path.Combine(Directory.GetCurrentDirectory(), "configs", "ip2region.xdb"),
        "configs/ip2region.xdb"
    };

    private readonly ILogger<Ip2RegionService> _logger;
    private readonly byte[]? _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ip2RegionService"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    public Ip2RegionService(ILogger<Ip2RegionService> logger)
    {
        _logger = logger;

        try
        {
            var path = CandidatePaths.FirstOrDefault(File.Exists);
            if (path == null)
            {
                logger.LogWarning("ip2region.xdb 数据文件不存在，IP 归属地解析与异地登录检测不可用（可将 xdb 放置于 configs/ip2region.xdb）.");
                return;
            }

            var data = File.ReadAllBytes(path);
            if (data.Length < HeaderLength + VectorIndexLength)
            {
                logger.LogWarning("ip2region.xdb 文件不完整（{Size} 字节），IP 归属地解析不可用.", data.Length);
                return;
            }

            // 头校验：结构版本 3.0（uint16 LE）且 IP 版本为 IPv4（偏移 16）
            var structureVersion = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
            var ipVersion = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(16, 2));
            if (structureVersion != 3 || ipVersion != 4)
            {
                logger.LogWarning("ip2region.xdb 结构版本不支持（structure={Structure}, ipVersion={IpVersion}），IP 归属地解析不可用.", structureVersion, ipVersion);
                return;
            }

            _data = data;
            logger.LogInformation("ip2region.xdb 已加载：{Path}（{Size} 字节）.", path, data.Length);
        }
        catch (Exception ex)
        {
            _data = null;
            logger.LogError(ex, "ip2region.xdb 加载失败，IP 归属地解析不可用.");
        }
    }

    /// <inheritdoc/>
    public bool IsAvailable => _data != null;

    /// <inheritdoc/>
    public IpLocationInfo Resolve(string ip)
    {
        if (_data == null || string.IsNullOrWhiteSpace(ip) || !System.Net.IPAddress.TryParse(ip, out var parsed))
        {
            return new IpLocationInfo();
        }

        // Kestrel 双栈监听下 IPv4 请求常表现为 ::ffff:a.b.c.d，先映射回 IPv4
        if (parsed.IsIPv4MappedToIPv6)
        {
            parsed = parsed.MapToIPv4();
        }

        // 仅支持 IPv4；IPv6 返回空（当前使用 v4 数据）
        if (parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return new IpLocationInfo();
        }

        try
        {
            var bytes = parsed.GetAddressBytes();
            if (bytes.Length != 4)
            {
                return new IpLocationInfo();
            }

            var ipValue = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            var region = Search(ipValue);
            return ParseRegion(region);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IP 归属地解析失败：{Ip}.", ip);
            return new IpLocationInfo();
        }
    }

    /// <summary>
    /// 向量索引定位 + 段索引二分查找，返回归属地原文（国家|省份|城市|ISP|国家码）.
    /// </summary>
    private string Search(uint ipValue)
    {
        var il0 = (int)(ipValue >> 24) & 0xFF;
        var il1 = (int)(ipValue >> 16) & 0xFF;

        // 定位向量索引：前两段八位组 → 段索引区 [sPtr, ePtr]
        var idx = HeaderLength + (il0 * 256 * 8) + (il1 * 8);
        var sPtr = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(idx, 4));
        var ePtr = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(idx + 4, 4));
        if (sPtr == 0 || ePtr == 0)
        {
            return string.Empty;
        }

        // 段索引二分查找
        int low = 0, high = (int)((ePtr - sPtr) / SegmentIndexSize);
        while (low <= high)
        {
            var middle = (low + high) >> 1;
            var pos = (int)sPtr + (middle * SegmentIndexSize);

            var segmentStart = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(pos, 4));
            if (ipValue < segmentStart)
            {
                high = middle - 1;
                continue;
            }

            var segmentEnd = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(pos + 4, 4));
            if (ipValue > segmentEnd)
            {
                low = middle + 1;
                continue;
            }

            var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(pos + 8, 2));
            var dataPtr = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(pos + 10, 4));
            return dataLength == 0 ? string.Empty : Encoding.UTF8.GetString(_data, (int)dataPtr, dataLength);
        }

        return string.Empty;
    }

    /// <summary>
    /// 解析 xdb 归属地串（国家|省份|城市|ISP|国家码），「0」与「Reserved」按空处理.
    /// </summary>
    private static IpLocationInfo ParseRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return new IpLocationInfo();
        }

        var parts = region.Split('|');
        string Get(int index)
        {
            if (parts.Length <= index)
            {
                return string.Empty;
            }

            var value = parts[index].Trim();
            return value is "0" or "Reserved" ? string.Empty : value;
        }

        return new IpLocationInfo
        {
            Country = Get(0),
            Province = Get(1),
            City = Get(2),
            Isp = Get(3)
        };
    }
}
