using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using MoAI.Gateway.Services;

namespace MoAI.Gateway;

/// <summary>
/// GET /v1/models：返回团队可用的对话模型（OpenAI list 格式）.
/// </summary>
public static class GatewayModelsEndpoint
{
    /// <summary>
    /// 处理请求.
    /// </summary>
    /// <param name="http">http 上下文.</param>
    /// <param name="resolver">模型解析服务.</param>
    /// <returns>返回任务.</returns>
    public static async Task HandleAsync(HttpContext http, GatewayModelResolver resolver)
    {
        var teamId = int.TryParse(http.User.FindFirst("teamid")?.Value, out var t) ? t : 0;
        var views = await resolver.ListAsync(teamId, http.RequestAborted);

        var data = new JsonArray();
        foreach (var view in views.GroupBy(x => x.Model.ModelId, StringComparer.Ordinal).Select(g => g.First()))
        {
            if (!string.Equals(view.Model.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            data.Add(new JsonObject
            {
                ["id"] = view.Model.ModelId,
                ["object"] = "model",
                ["created"] = view.Model.CreateTime.ToUnixTimeSeconds(),
                ["owned_by"] = view.Channel.ProviderKey,
            });
        }

        http.Response.StatusCode = 200;
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync(new JsonObject { ["object"] = "list", ["data"] = data }.ToJsonString(), http.RequestAborted);
    }
}
