using Microsoft.KernelMemory.Pipeline;

namespace MoAI.Wiki.Helpers;

public static class DocumentTypeHelper
{
    private static readonly MimeTypesDetection _mimeTypesDetection = new MimeTypesDetection();

    /// <summary>
    /// 知识库是否支持该文件类型.
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="mimeType"></param>
    /// <returns></returns>
    public static bool IsSupportedDocumentType(string filename, out string? mimeType)
    {
        return _mimeTypesDetection.TryGetFileType(filename, out mimeType);
    }
}
