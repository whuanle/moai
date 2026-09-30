using System.Net;
using System.Net.Sockets;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 拨测插件的内网防护：判断地址是否属于内网（回环/RFC1918/链路本地/CGNAT/未指定地址/IPv6 ULA 与链路本地），
/// 防止拨测插件被用来探测内网拓扑；实例配置 AllowPrivateNetwork=true 时由插件放行.
/// </summary>
internal static class HttpProbeGuard
{
    /// <summary>
    /// 判断地址是否属于内网.
    /// </summary>
    /// <param name="address">目标地址（IPv4-mapped IPv6 会被展开后再判定）.</param>
    /// <returns>true 表示内网地址.</returns>
    public static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateIPv4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPrivateIPv6(address.GetAddressBytes()),
            _ => false,
        };
    }

    private static bool IsPrivateIPv4(byte[] b)
    {
        return b[0] == 0
            || b[0] == 10
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    private static bool IsPrivateIPv6(byte[] b)
    {
        return (b[0] & 0xFE) == 0xFC
            || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80);
    }
}
