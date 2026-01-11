#pragma warning disable CA1822 // 将成员标记为 static

namespace MoAI.Plugin.Plugins;

public interface IPaddleocrPlugin
{
    Task<(IReadOnlyCollection<string> Texts, IReadOnlyCollection<string> Images)> OcrAsync(string base64, string @params);
}
