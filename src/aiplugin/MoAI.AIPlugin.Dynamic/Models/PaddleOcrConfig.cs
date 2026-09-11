using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PaddleOCR 系列动态插件配置（每个实例独立保存）.
/// </summary>
/// <remarks>
/// PaddleOCR 通常由用户在自己的 AIStudio 环境里独立部署，因此 <see cref="ApiUrl"/> 按实例保存；
/// 部分部署要求在 <c>Authorization</c> 头传 <c>token {Token}</c>，未开启鉴权时 <see cref="Token"/> 留空.
/// </remarks>
public class PaddleOcrConfig
{
    /// <summary>
    /// PaddleOCR 服务 API 地址，例如 <c>https://{id}.aistudio-app.com</c>.
    /// </summary>
    [Description("PaddleOCR 服务 API 地址，默认官方 AIStudio 部署；用户自部署时填写自己的服务地址")]
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// PaddleOCR 服务 Token；留空表示部署未开启鉴权.
    /// </summary>
    [Description("PaddleOCR 服务 Token；未开启鉴权时留空；调用时会作为 Authorization: token {Token} 发送")]
    public string Token { get; set; } = string.Empty;
}