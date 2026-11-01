namespace MoAI.LoginSafety.Models;

/// <summary>
/// 登录风控规则编码（持久化用稳定字符串，见 docs/loginsafety/sdd.md）.
/// </summary>
public static class LoginRiskRule
{
    /// <summary>
    /// R1：同一 IP 高频登录失败（爆破），自动封禁.
    /// </summary>
    public const string IpBruteForce = "R1";

    /// <summary>
    /// R2：同一 IP 对多个不同用户名登录失败（撞库喷洒），自动封禁.
    /// </summary>
    public const string IpSpray = "R2";

    /// <summary>
    /// R3：用户从新 IP 登录成功.
    /// </summary>
    public const string NewIp = "R3";

    /// <summary>
    /// R4：用户在非常驻属地登录成功（异地）.
    /// </summary>
    public const string NewLocation = "R4";

    /// <summary>
    /// R5：与上次成功登录属地不同且间隔过短（疾速旅行）.
    /// </summary>
    public const string ImpossibleTravel = "R5";

    /// <summary>
    /// R6：管理员/root 账号在新环境登录（升级处置）.
    /// </summary>
    public const string AdminNewEnvironment = "R6";

    /// <summary>
    /// 规则编码 → 面向管理员/邮件的标题.
    /// </summary>
    public static string GetTitle(string ruleCode) => ruleCode switch
    {
        IpBruteForce => "IP 登录失败过多（爆破）",
        IpSpray => "IP 多账号撞库（喷洒）",
        NewIp => "新 IP 登录",
        NewLocation => "异地登录",
        ImpossibleTravel => "疾速旅行（间隔过短的异地登录）",
        AdminNewEnvironment => "管理员账号新环境登录",
        _ => "未知规则"
    };
}
