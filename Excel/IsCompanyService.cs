using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;

namespace Coflnet.Excel;

public interface IIsCompanyService
{
    Task<Dictionary<string, bool>> CheckBatch(List<string> companies);
}

public class IsCompanyService : IIsCompanyService
{
    private IOpenAIService openAIService;
    private IDistributedCache cache;
    private ILogger<IsCompanyService> logger;

    public IsCompanyService(IOpenAIService openAIService, ILogger<IsCompanyService> logger, IDistributedCache cache)
    {
        this.openAIService = openAIService;
        this.logger = logger;
        this.cache = cache;
    }

    public async Task<Dictionary<string, bool>> CheckBatch(List<string> companies)
    {
        var result = new Dictionary<string, bool>();
        await Parallel.ForEachAsync(companies, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, async (company, token) =>
        {
            result.Add(company, (await PromptAi(company)).IsBrand);
        });
        return result;
    }

    public async Task<Response> PromptAi(string companyName)
    {
        var fromCache = await cache.GetStringAsync(companyName);
        if (fromCache != null)
        {
            return JsonConvert.DeserializeObject<Response>(fromCache);
        }
        var response = await openAIService.ChatCompletion.CreateCompletion(new OpenAI.ObjectModels.RequestModels.ChatCompletionCreateRequest()
        {
            Model = "gpt-4o-mini",
            Messages = new List<ChatMessage>()
            {
                ChatMessage.FromSystem("Du wurdest gefragt, ob folgendes eine webetreibende Marke ist"),
                ChatMessage.FromUser(companyName)
            },
            ResponseFormat = new ResponseFormat()
            {
                Type = StaticValues.CompletionStatics.ResponseFormat.JsonSchema,
                JsonSchema = new()
                {
                    Name = "extracted",
                    Strict = false,
                    Schema = PropertyDefinition.DefineObject(
                new Dictionary<string, PropertyDefinition>
                {
                    {
                        "isBrand",PropertyDefinition.DefineBoolean("Ist eine werbende Marke")
                },{
                        "formatted",PropertyDefinition.DefineString("Ausgeschriebener Markenname")
                },{
                        "description",PropertyDefinition.DefineString("Brand description")
                } }, new List<string> { "isBrand" },
                false,
                null,
                null)
                }
            }
        });
        if (response.Successful)
        {
            Console.WriteLine(response.Choices.First().Message.Content);
            await cache.SetStringAsync(companyName, response.Choices.First().Message.Content);
            return JsonConvert.DeserializeObject<Response>(response.Choices.First().Message.Content);
        }
        else
        {
            logger.LogError("Failed to get completion from OpenAI {error}", JsonConvert.SerializeObject(response.Error));
            return new();
        }

    }

    public class Response
    {
        public bool IsBrand { get; set; }
        public string Formatted { get; set; }
    }
}