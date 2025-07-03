// <copyright file="RabbitMQModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.Infra;

namespace MoAI.Modules;

public class MessageModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
        SystemOptions _systemOptions = context.Configuration.Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");

        //context.Services.AddMaomiMQ(
        //    (MqOptionsBuilder options) =>
        //    {
        //        options.WorkId = 1;
        //        options.AutoQueueDeclare = true;
        //        options.AppName = "MoAI";
        //        options.Rabbit = (ConnectionFactory options) =>
        //        {
        //            options.Uri = new Uri(_systemOptions.RabbitMQ!);
        //            options.ConsumerDispatchConcurrency = 100;
        //            options.ClientProvidedName = Assembly.GetExecutingAssembly().GetName().Name;
        //        };
        //    },
        //    context.Modules.Select(x => x.Assembly).ToArray(),
        //    [new ConsumerTypeFilter(), new EventBusTypeFilter(), new MediatrTypeFilter()]);
    }
}
