using Maomi;
using MoAI.AiModel.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.AiModel.Core;

[InjectModule<AiModelApiModule>]
public class AiModelCoreModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
    }
}