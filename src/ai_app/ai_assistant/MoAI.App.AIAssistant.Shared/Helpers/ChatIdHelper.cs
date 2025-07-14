// <copyright file="ChatIdHelper.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.App.AIAssistant.Helpers;

public static class ChatIdHelper
{
    public static string GetChatKey(long chatId)
    {
        return $"chat:{chatId}";
    }

    public static string GetChatHash(long chatId)
    {
        return chatId.ToString("x16");
    }

    public static bool TryParseId(string chatHash,out long chatId)
    {
        if (string.IsNullOrWhiteSpace(chatHash) || chatHash.Length != 16)
        {
            throw new ArgumentException("Invalid chat hash format.", nameof(chatHash));
        }

        return long.TryParse(chatHash, System.Globalization.NumberStyles.HexNumber, null, out chatId);
    }
}
