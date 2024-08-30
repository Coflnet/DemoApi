using Newtonsoft.Json;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;

namespace Coflnet.Tab;

public class AIPromtService
{

    private IOpenAIService openAIService;
    private ILogger<AIPromtService> logger;

    public AIPromtService(IOpenAIService openAIService, ILogger<AIPromtService> logger)
    {
        this.openAIService = openAIService;
        this.logger = logger;
    }

    public async Task<Dictionary<string, object>[]> PromptAi(string text, Dictionary<string, PropertyInfo> columnWithDescription, List<string> requiredColumns)
    {
        var response = await openAIService.ChatCompletion.CreateCompletion(new OpenAI.ObjectModels.RequestModels.ChatCompletionCreateRequest()
        {
            Model = "gpt-4o-mini",
            Messages = new List<ChatMessage>()
            {
                ChatMessage.FromSystem("You are a data extractor. Select data from the text into columns, if none match use ommit the column"),
                ChatMessage.FromUser(text)
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
                        "lines",PropertyDefinition.DefineArray(
                            PropertyDefinition.DefineObject(
                                columnWithDescription.ToDictionary(c => c.Key, c =>
                                {
                                    var def = new PropertyDefinition()
                                    {
                                        Type = PropertyDefinition.ConvertTypeToString(c.Value.Type),
                                        Description = c.Value.Description
                                    };
                                    if (c.Value.EnumValues?.Count > 0)
                                        def.Enum = c.Value.EnumValues;
                                    // make optional 
                                    return def;
                                }),
                                requiredColumns,
                                true,
                                null,
                                null
                            )
                        )
                } }, new List<string> { "lines" },
                false,
                "Response containing one line per entry",
                null)
                }
            }
        });
        if (response.Successful)
        {
            Console.WriteLine(response.Choices.First().Message.Content);
            return JsonConvert.DeserializeObject<Response>(response.Choices.First().Message.Content).Lines;
        }
        else
        {
            logger.LogError("Failed to get completion from OpenAI {error}", JsonConvert.SerializeObject(response.Error));
            return new Dictionary<string, object>[0];
        }

    }

    public class Response 
    {
        public Dictionary<string, object>[] Lines { get; set; }
    }
}