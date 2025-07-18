using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

namespace Agent
{
    /// <summary>
    /// ActSelectorAgent selects an action type (ActType) with reasoning and intention.
    /// The result (ActSelectionResult) is used to create an ActParameterRequest,
    /// which is then passed to the appropriate ParameterAgent to generate ActParameterResult.
    ///
    /// Example usage:
    /// var selection = await actSelectorAgent.SelectActAsync(situation);
    /// var paramRequest = new ActParameterRequest {
    ///     Reasoning = selection.Reasoning,
    ///     Intention = selection.Intention,
    ///     ActType = selection.ActType
    /// };
    /// var paramResult = await parameterAgent.GenerateParametersAsync(paramRequest);
    /// </summary>
    public class ActSelectorAgent : GPT
    {
        private Actor actor;

        public ActSelectorAgent(Actor actor) : base()
        {
            this.actor = actor;
            SetActorName(actor.Name);
        }

        public class ActSelectionResult
        {
            [JsonProperty("act_type")]
            public ActionAgent.ActionType ActType { get; set; }

            [JsonProperty("reasoning")]
            public string Reasoning { get; set; } // 왜 이 Act를 골랐는지

            [JsonProperty("intention")]
            public string Intention { get; set; } // 이 Act로 무엇을 하려는지
        }

        /// <summary>
        /// 상황을 받아 Act만 선택하고, 선택 이유와 의도도 반환
        /// </summary>
        /// <param name="situation">상황 설명</param>
        /// <returns>ActSelectionResult</returns>
        public async UniTask<ActSelectionResult> SelectActAsync(string situation)
        {
            messages.Add(new UserChatMessage(situation));
            var response = await SendGPTAsync<ActSelectionResult>(messages, options);

            Debug.Log($"[ActSelectorAgent] Act: {response.ActType}, Reason: {response.Reasoning}, Intention: {response.Intention}");
            return response;
        }

        // GPT 옵션 및 스키마 정의 (추후 필요시 확장)
        private new readonly ChatCompletionOptions options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "act_selection_result",
                jsonSchema: BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""act_type"": {
                                    ""type"": ""string"",
                                    ""enum"": [
                                        ""MoveToArea"", ""MoveToEntity"", ""MoveAway"", ""TalkToNPC"", ""RespondToPlayer"", ""UseObject"", ""PickUpItem"", ""InteractWithObject"", ""InteractWithNPC"", ""ObserveEnvironment"", ""ScanArea"", ""Wait"", ""PerformActivity"", ""EnterBuilding""
                                    ],
                                    ""description"": ""Type of action to perform""
                                },
                                ""reasoning"": {
                                    ""type"": ""string"",
                                    ""description"": ""Reason for selecting this action""
                                },
                                ""intention"": {
                                    ""type"": ""string"",
                                    ""description"": ""What the agent intends to achieve with this action""
                                }
                            },
                            ""required"": [""act_type"", ""reasoning"", ""intention""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }
} 