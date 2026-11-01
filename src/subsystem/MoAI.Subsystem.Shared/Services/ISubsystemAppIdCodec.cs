namespace MoAI.Subsystem.Services;

/// <summary>
/// 系统级应用接入 appid 编解码器（结构化自校验，公网防扫描）：
/// appid = <c>moai-sa-</c> + Base64Url(Guid v7 16B + HMAC-SHA256 截断 4B) = 35 字符；
/// HMAC key 由 SystemOptions.AES 派生——本地即可校验真伪并反解主键 Guid，
/// 伪造/打错/扫描请求不打数据库（蓄意构造合法 appid 需 2^32 次本地尝试/条）.
/// 存量随机格式（40 字符）不兼容本编码，由中间件按长度分派走 Key 等值查询.
/// </summary>
public interface ISubsystemAppIdCodec
{
    /// <summary>
    /// 为指定接入主键生成结构化 appid（Guid 编入 appid，HMAC 绑定，无需查库预检）.
    /// </summary>
    /// <param name="id">接入主键（实体 Id）.</param>
    /// <returns>返回 appid 原文.</returns>
    string New(Guid id);

    /// <summary>
    /// 校验结构化 appid 的 HMAC 并反解主键 Guid；格式不符/签名不匹配返回 false.
    /// </summary>
    /// <param name="key">待检 appid.</param>
    /// <param name="id">反解出的接入主键.</param>
    /// <returns>校验通过返回 true.</returns>
    bool TryDecode(string key, out Guid id);
}
