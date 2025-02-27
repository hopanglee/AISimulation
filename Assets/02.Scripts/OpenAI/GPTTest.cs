using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

public class GPTTest : MonoBehaviour
{
    public void Click()
    {
        var agent = new TestAgent();
        agent.Test();
    }
}

public class TestAgent : GPT
{
    public class MathReasoning
    {
        [JsonProperty("steps")]
        public List<Step> Steps { get; set; }

        [JsonProperty("final_answer")]
        public string FinalAnswer { get; set; }
    }

    public class Step
    {
        [JsonProperty("explanation")]
        public string Explanation { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }
    }

    public TestAgent()
        : base()
    {
        messages = new List<ChatMessage>()
        {
            new SystemChatMessage("You're the good Assistant for user."),
        };

        options = new()
        {
            Tools = { getCurrentLocationTool, getCurrentWeatherTool }, // tool
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat( // structured output
                jsonSchemaFormatName: "math_reasoning",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"
            {
                ""type"": ""object"",
                ""properties"": {
                    ""steps"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""explanation"": { ""type"": ""string"" },
                                ""output"": { ""type"": ""string"" }
                            },
                            ""required"": [""explanation"", ""output""],
                            ""additionalProperties"": false
                        }
                    },
                    ""final_answer"": { ""type"": ""string"" }
                },
                ""required"": [""steps"", ""final_answer""],
                ""additionalProperties"": false
            }
        "
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        switch (toolCall.FunctionName)
        {
            case nameof(GetCurrentLocation):
            {
                string toolResult = GetCurrentLocation();
                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));

                break;
            }

            case nameof(GetCurrentWeather):
            {
                // The arguments that the model wants to use to call the function are specified as a
                // stringified JSON object based on the schema defined in the tool definition. Note that
                // the model may hallucinate arguments too. Consequently, it is important to do the
                // appropriate parsing and validation before calling the function.
                using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                bool hasLocation = argumentsJson.RootElement.TryGetProperty(
                    "location",
                    out JsonElement location
                );
                bool hasUnit = argumentsJson.RootElement.TryGetProperty(
                    "unit",
                    out JsonElement unit
                );

                if (!hasLocation)
                {
                    throw new System.ArgumentNullException(
                        nameof(location),
                        "The location argument is required."
                    );
                }

                string toolResult = hasUnit
                    ? GetCurrentWeather(location.GetString(), unit.GetString())
                    : GetCurrentWeather(location.GetString());
                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                break;
            }

            default:
            {
                // Handle other unexpected calls.
                throw new NotImplementedException();
            }
        }
    }

    private static string GetCurrentLocation()
    {
        // Call the location API here.
        Debug.Log($"GetCurrentLocation Call");
        return "San Francisco";
    }

    private static string GetCurrentWeather(string location, string unit = "celsius")
    {
        // Call the weather API here.
        Debug.Log($"GetCurrentWeather Call");
        return $"31 {unit}";
    }

    private static readonly ChatTool getCurrentLocationTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCurrentLocation),
        functionDescription: "Get the user's current location"
    );

    private static readonly ChatTool getCurrentWeatherTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCurrentWeather),
        functionDescription: "Get the current weather in a given location",
        functionParameters: BinaryData.FromBytes(
            System.Text.Encoding.UTF8.GetBytes(
                @"{
            ""type"": ""object"",
            ""properties"": {
                ""location"": {
                    ""type"": ""string"",
                    ""description"": ""The city and state, e.g. Boston, MA""
                },
                ""unit"": {
                    ""type"": ""string"",
                    ""enum"": [ ""celsius"", ""fahrenheit"" ],
                    ""description"": ""The temperature unit to use. Infer this from the specified location.""
                }
            },
            ""required"": [ ""location"" ]
        }"
            )
        )
    );

    public async void Test()
    {
        messages.Add(new UserChatMessage("What's the weather like today?"));
        var output = await SendGPTAsync<MathReasoning>(messages, options);

        for (int i = 0; i < output.Steps.Count; i++)
        {
            Debug.Log(
                $@"{i}. 
            Explanation : {output.Steps[i].Explanation}
            Output : {output.Steps[i].Output}"
            );
        }

        Debug.Log($"Final Answer : {output.FinalAnswer}");
    }
}
