using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Feishu.Models;

public class WikiFeishuConfig: IWikiPluginKey
{
    public string AppId { get; set; }

    public string AppSecret { get; set; }

    public string SpaceId { get; set; }

    public string ParentNodeToken { get; set; }

    public string PluginKey => "feishu";
}
