// <copyright file="AppConst.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Diagnostics;

namespace MoAI.Infra;

/// <summary>
/// 系统常用信息.
/// </summary>
public static class AppConst
{
    /// <summary>
    /// 程序根路径.
    /// </summary>
    public static string AppPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase!;

    /// <summary>
    /// 配置文件路径.
    /// </summary>
    public static string ConfigsPath = Path.Combine(AppPath, "configs");

    /// <summary>
    /// 配置文件模板路径.
    /// </summary>
    public static string ConfigsTemplate = Path.Combine(AppPath, "configs_template");

    public static string PrivateRSA = Path.Combine(ConfigsPath, "rsa_private.key");

    /// <summary>
    /// 不在业务逻辑中发生的日志，统一使用这个名称做日志命名.<br />
    /// 例如在模块类中的打印的日志.
    /// </summary>
    public const string LoggerName = "MoAI";

    public static readonly ActivitySource ActivitySource = new ActivitySource("MoAI");
}
