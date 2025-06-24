using MaomiAI.Infra;

namespace MaomiAI
{
    [InjectModule<InfraCoreModule>]
    public class DBModule : IModule
    {
        public void ConfigureServices(ServiceContext context)
        {
        }
    }
}