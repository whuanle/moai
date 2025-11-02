using Maomi;
using MoAI.Infra;

namespace MoAI.Storage;

/// <summary>
/// StorageLocalModule.
/// </summary>
public class StorageLocalModule : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageLocalModule"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public StorageLocalModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        if (!Directory.Exists(_systemOptions.Storage.LocalPath))
        {
            Directory.CreateDirectory(_systemOptions.Storage.LocalPath);
        }
    }
}
