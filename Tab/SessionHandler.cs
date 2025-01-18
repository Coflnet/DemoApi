using Coflnet.Core;
using Newtonsoft.Json;
using RestSharp;
using System.Collections.Concurrent;

namespace Coflnet.Tab;

public class SessionHandler
{
    private ConcurrentDictionary<string, SessionState> sessions = new();
    private IConfiguration configuration;
    private ILogger<SessionHandler> logger;
    private AIPromtService promtService;

    public SessionHandler(IConfiguration configuration, ILogger<SessionHandler> logger, AIPromtService promtService)
    {
        this.configuration = configuration;
        this.logger = logger;
        this.promtService = promtService;
    }

    public SessionState GetSession(string sessionId)
    {
        return sessions.GetOrAdd(sessionId, (k) => new());
    }

    public async Task<RecognitionResponse> Recognize(RecognitionRequest request)
    {
        var session = GetSession(request.SessionId);
        var shouldFinish = request.Base64Opus == null;
        if (!shouldFinish)
        {
            var text = await GetTextFromAudio(request.Base64Opus, request.Language);
            session.Texts.Add(text);
        }
        var fullText = string.Join("\n", session.Texts);
        var response = await promtService.PromptAi(fullText, request.ColumnWithDescription, new());
        if (response.IsComplete || shouldFinish)
        {
            session.Texts.Clear();
        }
        return new() { ColumnWithText = response.Lines, IsComplete = response.IsComplete, Text = fullText };
    }

    public async Task<string> GetTextFromAudio(string base64, string? language = null)
    {
        var restClient = new RestClient($"https://api.cloudflare.com/client/v4/accounts/{configuration["CLOUDFLARE_ACCOUNT"]}/ai/run/@cf/openai/whisper-large-v3-turbo");
        var request = new RestRequest("", Method.Post);
        request.AddHeader("Authorization", $"Bearer {configuration["CLOUDFLARE_API_KEY"]}");
        if (base64.StartsWith("data:audio/wav;base64,"))
        {
            base64 = base64.Substring("data:audio/wav;base64,".Length);
        }
        if (language == null)
            request.AddJsonBody(new { audio = base64 });
        else
            request.AddJsonBody(new { audio = base64, language = language });
        var response = await restClient.ExecuteAsync(request);
        var parsed = JsonConvert.DeserializeObject<RecogintionResponse>(response.Content);
        if (parsed?.Result?.Segments == null)
        {
            logger.LogError("Error while parsing response: {code} {response}", response.StatusCode, response.Content);
            logger.LogInformation("Sent request: {base64}", base64.Truncate(100));
            return "";
        }
        var fullText = string.Join("\n", parsed.Result.Segments.Select(r => r?.Text).Where(r => !string.IsNullOrWhiteSpace(r)));
        logger.LogInformation($"Received response: {fullText}");
        return fullText;
    }

    public class SessionState
    {
        public List<string> Texts { get; set; } = new();
    }
}
