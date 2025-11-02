namespace MoAI.Infra.Service;

/// <summary>
/// IAESProvider.
/// </summary>
public interface IAESProvider
{
    /// <summary>
    /// 解密.
    /// </summary>
    /// <param name="cipherText"></param>
    /// <returns></returns>
    string Decrypt(string cipherText);

    /// <summary>
    /// 加密.
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns></returns>
    string Encrypt(string plainText);
}