using FastEndpoints;
using MoAI.Wiki.Plugins.Crawler.Commands;

namespace MoAI.EndpointsTest;

// todo: 绕过 fastendpoint，后期
[HttpPost("/aaaaaaaaaaaaaaaaaaaaaaa")]
public class AAAEndpoint : Endpoint<AddWikiCrawlerConfigCommand, int>
{

    public override async Task<int> ExecuteAsync(AddWikiCrawlerConfigCommand req, CancellationToken ct)
    {
        return 1;
    }
}
