namespace MoAI.Subsystem.Services;

/// <summary>
/// 系统级接入密钥可逆加密服务（仅系统级行使用；团队级行仍为 PBKDF2 哈希）：
/// 存储格式 <c>v2:</c> 前缀 + AES 密文（可回显，飞书式随时查看/复制）；
/// 无前缀 = 存量 PBKDF2 哈希（不可回显，验证仍走哈希，重置后转可查看格式）.
/// </summary>
public interface ISubsystemSecretCipher
{
    /// <summary>
    /// 生成新密钥原文（48 位随机字符）.
    /// </summary>
    /// <returns>返回密钥原文.</returns>
    string NewSecret();

    /// <summary>
    /// 生成展示掩码（前 4 + **** + 后 4，长度不足 12 时整段 ****）.
    /// </summary>
    /// <param name="secret">密钥原文.</param>
    /// <returns>返回掩码.</returns>
    string Mask(string secret);

    /// <summary>
    /// 加密为可回显存储格式（v2: 前缀 + AES 密文）.
    /// </summary>
    /// <param name="secret">密钥原文.</param>
    /// <returns>返回库内存储值.</returns>
    string EncryptForStorage(string secret);

    /// <summary>
    /// 校验输入密钥与库内存储值是否匹配（按存储格式分派：v2 可逆比对 / 存量 PBKDF2 哈希校验）.
    /// </summary>
    /// <param name="stored">库内存储值.</param>
    /// <param name="salt">库内盐（存量 PBKDF2 行使用，v2 行为空）.</param>
    /// <param name="input">用户输入的密钥原文.</param>
    /// <returns>匹配返回 true.</returns>
    bool Verify(string stored, string salt, string input);

    /// <summary>
    /// 尝试回显密钥原文（仅 v2 可逆格式；存量 PBKDF2 哈希不可回显返回 false）.
    /// </summary>
    /// <param name="stored">库内存储值.</param>
    /// <param name="secret">输出密钥原文.</param>
    /// <returns>可回显返回 true.</returns>
    bool TryReveal(string stored, out string secret);
}
