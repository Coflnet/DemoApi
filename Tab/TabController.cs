using FFMpegCore;
using Microsoft.AspNetCore.Mvc;
using OpenAI.ObjectModels.SharedModels;
using RestSharp;
using System.IO;

namespace Coflnet.Tab;

[ApiController]
[Route("api/[controller]")]
public class TabController : ControllerBase
{
    private AIPromtService promtService;

    public TabController(AIPromtService promtService)
    {
        this.promtService = promtService;
    }

    [HttpPost]
    public async Task<Dictionary<string, string>[]> Post(TabRequest request)
    {
        return await promtService.PromptAi(request.Text, request.ColumnWithDescription, request.RequiredColums);
    }
}

public class TabRequest
{
    public string Text { get; set; }
    public Dictionary<string, PropertyInfo> ColumnWithDescription { get; set; }
    public List<string> RequiredColums { get; set; }
}

public class PropertyInfo
{
    public PropertyDefinition.FunctionObjectTypes Type { get; set; }
    public string? Description { get; set; }
    public List<string>? EnumValues { get; set; }
}
