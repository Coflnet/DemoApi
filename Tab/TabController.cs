using FFMpegCore;
using Microsoft.AspNetCore.Mvc;
using OpenAI.ObjectModels.SharedModels;
using System.IO;

namespace Coflnet.Tab;

[ApiController]
[Route("api/[controller]")]
public class TabController : ControllerBase
{
    private AIPromtService promtService;
    private SessionHandler sessionHandler;

    public TabController(AIPromtService promtService, SessionHandler sessionHandler)
    {
        this.promtService = promtService;
        this.sessionHandler = sessionHandler;
    }

    [HttpPost]
    public async Task<Dictionary<string, string>[]> Post(TabRequest request)
    {
        return (await promtService.PromptAi(request.Text, request.ColumnWithDescription, request.RequiredColums)).Lines;
    }

    [HttpPost]
    [Route("recognize")]
    public async Task<RecognitionResponse> Recognize(RecognitionRequest request)
    {
        return await sessionHandler.Recognize(request);
    }
}

public class RecognitionRequest
{
    public string Base64Opus { get; set; }
    public string Language { get; set; }
    public string SessionId { get; set; }
    public Dictionary<string, PropertyInfo> ColumnWithDescription { get; set; }
}

public class RecognitionResponse
{
    public bool IsComplete { get; set; }
    public string Text { get; set; }
    public Dictionary<string, string>[] ColumnWithText { get; set; }
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
