// <copyright file="IAESProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

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