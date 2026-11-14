namespace MoAI.Subsystem.Client.Tests;

/// <summary>捕获的请求快照（原 HttpRequestMessage 在发送后即释放）。</summary>
internal sealed class CapturedRequest
{
    public string Method { get; private init; } = string.Empty;

    public Uri Uri { get; private init; } = new("http://stub.local/", UriKind.Absolute);

    public string? Authorization { get; private init; }

    public string? Body { get; private init; }

    public static async Task<CapturedRequest> FromAsync(HttpRequestMessage request)
    {
        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync();
        }

        return new CapturedRequest
        {
            Method = request.Method.ToString(),
            Uri = request.RequestUri!,
            Authorization = request.Headers.Authorization?.ToString(),
            Body = body,
        };
    }
}
