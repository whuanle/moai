using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

public class BoChaCode
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("msg")]
    public string Msg { get; init; }
}