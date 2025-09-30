using System;
using System.Collections.Generic;
using System.Linq;
using OpenAI.Chat;
using UnityEngine;
using Agent;
using PlanStructures;
using Memory;
using Newtonsoft.Json.Linq;
using GeminiFunction = Mscc.GenerativeAI.FunctionDeclaration;
using GeminiFuncionCall = Mscc.GenerativeAI.FunctionCall;
using System.Text.Json;

namespace Agent.Tools
{
    /// <summary>
    /// 중앙 집중식 도구 관리자
    /// 모든 도구들을 한 곳에서 관리하고, 필요한 도구들을 쉽게 조합할 수 있도록 함
    /// </summary>
    public static class ToolManager
    {
        // Helper: build JObject from UTF8 bytes (for readable verbatim JSON usage)
        private static JObject JsonFromUtf8Bytes(byte[] utf8)
        {
            if (utf8 == null || utf8.Length == 0) return null;
            var json = System.Text.Encoding.UTF8.GetString(utf8);
            return string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);
        }
        # region GPT Tool 정의
        public static class ToolDefinitions
        {
            public static readonly ChatTool SwapInventoryToHand = ChatTool.CreateFunctionTool(
                functionName: nameof(SwapInventoryToHand),
                functionDescription: "인벤토리의 아이템 이름을 지정해 손으로 옮기거나 교체합니다. 손이 비어 있으면 이동만 하고, 손에 아이템이 있으면 서로 교체합니다.",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""itemName"": {
                                    ""type"": ""string"",
                                    ""description"": ""손과 교체할 인벤토리 아이템의 이름""
                                }
                            },
                            ""required"": [""itemName""]
                        }"
                    )
                )
            );

            public static readonly ChatTool GetPaymentPriceList = ChatTool.CreateFunctionTool(
                functionName: nameof(GetPaymentPriceList),
                functionDescription: "이 NPC가 결제 가능한 작업에 대해 제공하는 가격표를 이름-가격 쌍으로 반환합니다. 미지원 시 안내 메시지를 반환합니다."
            );


            public static readonly ChatTool GetWorldAreaInfo = ChatTool.CreateFunctionTool(
                functionName: nameof(GetWorldAreaInfo),
                functionDescription: "월드의 모든 에리어와 이들 간 연결 정보를 반환합니다"
            );

            public static readonly ChatTool GetUserMemory = ChatTool.CreateFunctionTool(
                functionName: nameof(GetUserMemory),
                functionDescription: "에이전트의 메모리(최근 사건, 관찰, 대화 등)를 조회합니다"
            );

            public static readonly ChatTool GetShortTermMemory = ChatTool.CreateFunctionTool(
                functionName: nameof(GetShortTermMemory),
                functionDescription: "필터 옵션과 함께 최근 단기 기억을 조회합니다",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""memoryType"": {
                                    ""type"": ""string"",
                                    ""description"": ""메모리 유형(perception, action_start, action_complete, plan_created 등)으로 필터링합니다. 비우면 모든 유형입니다."",
                                    ""enum"": ["""", ""perception"", ""action_start"", ""action_complete"", ""plan_created"", ""conversation""]
                                },
                                ""limit"": {
                                    ""type"": ""integer"",
                                    ""description"": ""최대 반환 개수(기본 20, 최대 50)"",
                                    ""minimum"": 1,
                                    ""maximum"": 50
                                },
                                ""keyword"": {
                                    ""type"": ""string"",
                                    ""description"": ""해당 키워드를 포함하는 메모리만 반환""
                                }
                            },
                            ""required"": []
                        }"
                    )
                )
            );

            public static readonly ChatTool GetLongTermMemory = ChatTool.CreateFunctionTool(
                functionName: nameof(GetLongTermMemory),
                functionDescription: "장기 기억을 검색하여 반환합니다",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""searchQuery"": {
                                    ""type"": ""string"",
                                    ""description"": ""해당 질의를 포함하는 기억을 검색""
                                },
                                ""dateRange"": {
                                    ""type"": ""string"",
                                    ""description"": ""날짜 범위 필터(예: today, yesterday, this_week, last_week)"",
                                    ""enum"": [""today"", ""yesterday"", ""this_week"", ""last_week"", ""this_month"", ""all""]
                                },
                                ""limit"": {
                                    ""type"": ""integer"",
                                    ""description"": ""최대 반환 개수(기본 10, 최대 30)"",
                                    ""minimum"": 1,
                                    ""maximum"": 30
                                }
                            },
                            ""required"": []
                        }"
                    )
                )
            );

            public static readonly ChatTool GetMemoryStats = ChatTool.CreateFunctionTool(
                functionName: nameof(GetMemoryStats),
                functionDescription: "현재 메모리 상태 통계(개수, 최근 활동 등)를 조회합니다"
            );

            public static readonly ChatTool GetCurrentTime = ChatTool.CreateFunctionTool(
                functionName: nameof(GetCurrentTime),
                functionDescription: "현재 시뮬레이션 시간(연, 월, 일, 시, 분)을 조회합니다"
            );

            public static readonly ChatTool GetCurrentPlan = ChatTool.CreateFunctionTool(
                functionName: nameof(GetCurrentPlan),
                functionDescription: "현재 계획 정보(완료/진행/예정 작업)를 조회합니다"
            );

            public static readonly ChatTool GetCurrentSpecificAction = ChatTool.CreateFunctionTool(
                functionName: nameof(GetCurrentSpecificAction),
                functionDescription: "현재 시점에 수행해야 할 구체적인 행동을 조회합니다"
            );

            // 건물 이름으로 해당 건물이 속한 에리어 경로(상위-하위)를 ":"로 연결해 반환합니다. 예: "도쿄:신주쿠:카부키쵸:1-chome-5"
            public static readonly ChatTool FindBuildingAreaPath = ChatTool.CreateFunctionTool(
                functionName: nameof(FindBuildingAreaPath),
                functionDescription: "건물 이름을 받아 상위에서 말단까지 ':'로 연결된 에리어 경로를 반환합니다(예: '도쿄:신주쿠:카부키쵸:1-chome-5').",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""buildingName"": {
                                    ""type"": ""string"",
                                    ""description"": ""검색할 지역화된 건물 이름(예: '이자카야 카게츠')""
                                }
                            },
                            ""required"": [""buildingName""]
                        }"
                    )
                )
            );

            // 현재 액터의 위치 Area에서 목표 Area 키(이름 또는 전체경로)까지의 최단 Area 경로를 찾아 "A -> B -> C" 형식으로 반환
            public static readonly ChatTool FindShortestAreaPathFromActor = ChatTool.CreateFunctionTool(
                functionName: nameof(FindShortestAreaPathFromActor),
                functionDescription: "액터의 현재 에리어에서 목표 에리어 키(이름 또는 전체 경로)까지의 최단 연결 경로를 'A -> B -> C' 형식으로 반환합니다",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""targetAreaKey"": {
                                    ""type"": ""string"",
                                    ""description"": ""목표 에리어 키: locationName(예: '1-chome-5') 또는 전체 경로(예: '도쿄:신주쿠:카부키쵸:1-chome-5')""
                                }
                            },
                            ""required"": [""targetAreaKey""]
                        }"
                    )
                )
            );

            // 전체 월드 지역 위치 텍스트를 반환 (현재는 도쿄 기준 구조 텍스트 파일 반환)
            public static readonly ChatTool GetWorldAreaStructureText = ChatTool.CreateFunctionTool(
                functionName: nameof(GetWorldAreaStructureText),
                functionDescription: "11.GameDatas 기반의 월드 에리어 구조 텍스트를 반환합니다(예: tokyo_area_structure.txt)."
            );

            // 현재 액터의 location_memories.json 전체 반환
            public static readonly ChatTool GetActorLocationMemories = ChatTool.CreateFunctionTool(
                functionName: nameof(GetActorLocationMemories),
                functionDescription: "이 액터의 위치 기억(location_memories.json) 전체를 반환합니다"
            );

            // 현재 액터의 location_memories.json에서 주어진 범위/키로 필터링해 반환
            public static readonly ChatTool GetActorLocationMemoriesFiltered = ChatTool.CreateFunctionTool(
                functionName: nameof(GetActorLocationMemoriesFiltered),
                functionDescription: "이 액터의 위치 기억을 범위/키로 필터링해 반환합니다",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""areaKey"": {
                                    ""type"": ""string"",
                                    ""description"": ""범위 또는 정확한 키. 예: '도쿄', '도쿄:신주쿠', '신주쿠', '1-chome-1', '도쿄:신주쿠:카부키쵸:1-chome-1'""
                                }
                            },
                            ""required"": [""areaKey""]
                        }"
                    )
                )
            );

            // 특정 인물의 관계기억 요약을 불러옵니다 (actor.LoadRelationships(targetName) 사용)
            public static readonly ChatTool LoadRelationshipByName = ChatTool.CreateFunctionTool(
                functionName: nameof(LoadRelationshipByName),
                functionDescription: "특정 인물 이름에 대한 관계 기억 요약을 불러옵니다(actor.LoadRelationships(targetName) 사용)",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""targetName"": {
                                    ""type"": ""string"",
                                    ""description"": ""관계 기억을 불러올 대상의 정확한 이름""
                                }
                            },
                            ""required"": [""targetName""]
                        }"
                    )
                )
            );
        }
        #endregion

        #region  공급자-중립 도구 정의 (LLMToolSchema)
        public static class NeutralToolDefinitions
        {
            public static readonly LLMToolSchema SwapInventoryToHand = new LLMToolSchema
            {
                name = nameof(SwapInventoryToHand),
                description = "인벤토리의 아이템 이름을 지정해 손으로 옮기거나 교체합니다. 손이 비어 있으면 이동만 하고, 손에 아이템이 있으면 서로 교체합니다.",
                format = JsonFromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""itemName"": { ""type"": ""string"", ""description"": ""Name of the item in inventory to swap with hand"" }
                    },
                    ""required"": [""itemName""]
                }"))
            };

            public static readonly LLMToolSchema GetPaymentPriceList = new LLMToolSchema
            {
                name = nameof(GetPaymentPriceList),
                description = "이 NPC가 결제 가능한 작업에 대해 제공하는 가격표를 이름-가격 쌍으로 반환합니다. 미지원 시 안내 메시지를 반환합니다.",
                format = null
            };

            public static readonly LLMToolSchema GetWorldAreaInfo = new LLMToolSchema
            {
                name = nameof(GetWorldAreaInfo),
                description = "월드의 모든 에리어와 이들 간 연결 정보를 반환합니다",
                format = null
            };

            public static readonly LLMToolSchema GetUserMemory = new LLMToolSchema
            {
                name = nameof(GetUserMemory),
                description = "에이전트의 메모리(최근 사건, 관찰, 대화 등)를 조회합니다",
                format = null
            };

            public static readonly LLMToolSchema GetShortTermMemory = new LLMToolSchema
            {
                name = nameof(GetShortTermMemory),
                description = "필터 옵션과 함께 최근 단기 기억을 조회합니다",
                format = JsonFromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""memoryType"": { ""type"": ""string"", ""description"": ""Filter by memory type (perception, action_start, action_complete, plan_created, etc.). Leave empty for all types."", ""enum"": ["""", ""perception"", ""action_start"", ""action_complete"", ""plan_created"", ""conversation""] },
                        ""limit"": { ""type"": ""integer"", ""description"": ""Maximum number of memories to return (default: 20, max: 50)"", ""minimum"": 1, ""maximum"": 50 },
                        ""keyword"": { ""type"": ""string"", ""description"": ""Filter memories containing this keyword"" }
                    },
                    ""required"": []
                }"))
            };

            public static readonly LLMToolSchema GetLongTermMemory = new LLMToolSchema
            {
                name = nameof(GetLongTermMemory),
                description = "장기 기억을 검색하여 반환합니다",
                format = JsonFromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""searchQuery"": { ""type"": ""string"", ""description"": ""Search for memories containing this query"" },
                        ""dateRange"": { ""type"": ""string"", ""description"": ""Date range filter (e.g. 'today', 'yesterday', 'this_week', 'last_week')"", ""enum"": [""today"", ""yesterday"", ""this_week"", ""last_week"", ""this_month"", ""all""] },
                        ""limit"": { ""type"": ""integer"", ""description"": ""Maximum number of memories to return (default: 10, max: 30)"", ""minimum"": 1, ""maximum"": 30 }
                    },
                    ""required"": []
                }"))
            };

            public static readonly LLMToolSchema GetMemoryStats = new LLMToolSchema
            {
                name = nameof(GetMemoryStats),
                description = "현재 메모리 상태 통계(개수, 최근 활동 등)를 조회합니다",
                format = null
            };

            public static readonly LLMToolSchema GetCurrentTime = new LLMToolSchema
            {
                name = nameof(GetCurrentTime),
                description = "현재 시뮬레이션 시간(연, 월, 일, 시, 분)을 조회합니다",
                format = null
            };

            public static readonly LLMToolSchema GetCurrentPlan = new LLMToolSchema
            {
                name = nameof(GetCurrentPlan),
                description = "현재 계획 정보(완료/진행/예정 작업)를 조회합니다",
                format = null
            };

            public static readonly LLMToolSchema GetCurrentSpecificAction = new LLMToolSchema
            {
                name = nameof(GetCurrentSpecificAction),
                description = "현재 시점에 수행해야 할 구체적인 행동을 조회합니다",
                format = null
            };

            public static readonly LLMToolSchema FindBuildingAreaPath = new LLMToolSchema
            {
                name = nameof(FindBuildingAreaPath),
                description = "건물 이름을 받아 상위에서 말단까지 ':'로 연결된 에리어 경로를 반환합니다(예: '도쿄:신주쿠:카부키쵸:1-chome-5').",
                format = JsonFromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""buildingName"": { ""type"": ""string"", ""description"": ""Localized building name to search (e.g., '이자카야 카게츠')"" }
                    },
                    ""required"": [""buildingName""]
                }"))
            };

            public static readonly LLMToolSchema FindShortestAreaPathFromActor = new LLMToolSchema
            {
                name = nameof(FindShortestAreaPathFromActor),
                description = "액터의 현재 에리어에서 목표 에리어 키(이름 또는 전체 경로)까지의 최단 연결 경로를 'A -> B -> C' 형식으로 반환합니다",
                format = JsonFromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""targetAreaKey"": { ""type"": ""string"", ""description"": ""Target area key: either locationName (e.g., '1-chome-5') or full path (e.g., '도쿄:신주쿠:카부키쵸:1-chome-5')"" }
                    },
                    ""required"": [""targetAreaKey""]
                }"))
            };

            public static readonly LLMToolSchema GetWorldAreaStructureText = new LLMToolSchema
            {
                name = nameof(GetWorldAreaStructureText),
                description = "11.GameDatas 기반의 월드 에리어 구조 텍스트를 반환합니다(예: tokyo_area_structure.txt).",
                format = null
            };

            public static readonly LLMToolSchema GetActorLocationMemories = new LLMToolSchema
            {
                name = nameof(GetActorLocationMemories),
                description = "이 액터의 위치 기억(location_memories.json) 전체를 반환합니다",
                format = null
            };

            public static readonly LLMToolSchema GetActorLocationMemoriesFiltered = new LLMToolSchema
            {
                name = nameof(GetActorLocationMemoriesFiltered),
                description = "이 액터의 위치 기억을 범위/키로 필터링해 반환합니다",
                format = JsonFromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""areaKey"": { ""type"": ""string"", ""description"": ""Scope or exact key. Examples: '도쿄', '도쿄:신주쿠', '신주쿠', '1-chome-1', '도쿄:신주쿠:카부키쵸:1-chome-1'"" }
                    },
                    ""required"": [""areaKey""]
                }"))
            };

            public static readonly LLMToolSchema LoadRelationshipByName = new LLMToolSchema
            {
                name = nameof(LoadRelationshipByName),
                description = "특정 인물 이름에 대한 관계 기억 요약을 불러옵니다(actor.LoadRelationships(targetName) 사용)",
                format = JsonFromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""targetName"": { ""type"": ""string"", ""description"": ""Exact name of the person to load relationship memory for"" }
                    },
                    ""required"": [""targetName""]
                }"))
            };
        }
        #endregion

        #region 공급자-중립 도구 세트
        public static class NeutralToolSets
        {
            public static readonly LLMToolSchema[] ItemManagement = { NeutralToolDefinitions.SwapInventoryToHand };
            public static readonly LLMToolSchema[] Payment = { NeutralToolDefinitions.GetPaymentPriceList };
            public static readonly LLMToolSchema[] ActionInfo = { };
            public static readonly LLMToolSchema[] WorldInfo = { NeutralToolDefinitions.GetWorldAreaInfo, NeutralToolDefinitions.GetCurrentTime, NeutralToolDefinitions.FindBuildingAreaPath, NeutralToolDefinitions.FindShortestAreaPathFromActor, NeutralToolDefinitions.GetWorldAreaStructureText };
            public static readonly LLMToolSchema[] Memory = { NeutralToolDefinitions.GetUserMemory, NeutralToolDefinitions.GetShortTermMemory, NeutralToolDefinitions.GetLongTermMemory, NeutralToolDefinitions.GetMemoryStats, NeutralToolDefinitions.GetActorLocationMemories, NeutralToolDefinitions.GetActorLocationMemoriesFiltered, NeutralToolDefinitions.LoadRelationshipByName };
            public static readonly LLMToolSchema[] Plan = { NeutralToolDefinitions.GetCurrentPlan, NeutralToolDefinitions.GetCurrentSpecificAction };
            public static readonly LLMToolSchema[] All = { NeutralToolDefinitions.SwapInventoryToHand, NeutralToolDefinitions.GetWorldAreaInfo, NeutralToolDefinitions.GetUserMemory, NeutralToolDefinitions.GetShortTermMemory, NeutralToolDefinitions.GetLongTermMemory, NeutralToolDefinitions.GetMemoryStats, NeutralToolDefinitions.GetCurrentTime, NeutralToolDefinitions.GetCurrentPlan, NeutralToolDefinitions.GetCurrentSpecificAction, NeutralToolDefinitions.FindBuildingAreaPath, NeutralToolDefinitions.FindShortestAreaPathFromActor, NeutralToolDefinitions.LoadRelationshipByName };
        }
        #endregion

        // 변환/어댑터: LLMToolSchema -> OpenAI ChatTool
        public static ChatTool ToOpenAITool(LLMToolSchema schema)
        {
            if (schema == null) return null;
            if (schema.format == null)
            {
                return ChatTool.CreateFunctionTool(
                    functionName: schema.name,
                    functionDescription: schema.description
                );
            }
            return ChatTool.CreateFunctionTool(
                functionName: schema.name,
                functionDescription: schema.description,
                functionParameters: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(schema.format.ToString()))
            );
        }

        public static GeminiFunction ToGeminiTool(LLMToolSchema schema)
        {
            if (schema == null) return null;
            // if (schema.format == null)
            // {
            //     return ChatTool.CreateFunctionTool(
            //         functionName: schema.name,
            //         functionDescription: schema.description
            //     );
            // }
            // return ChatTool.CreateFunctionTool(
            //     functionName: schema.name,
            //     functionDescription: schema.description,
            //     functionParameters: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(schema.format.ToString()))
            // );
            return default;
        }

        public static void AddToolsToOptionsFromSchemas(ChatCompletionOptions options, params LLMToolSchema[] schemas)
        {
            if (options == null || schemas == null) return;
            foreach (var s in schemas)
            {
                var tool = ToOpenAITool(s);
                if (tool != null) options.Tools.Add(tool);
            }
        }

        // 도구 세트 정의
        public static class ToolSets
        {
            /// <summary>
            /// 아이템 관리 관련 도구들
            /// </summary>
            public static readonly ChatTool[] ItemManagement = { ToolDefinitions.SwapInventoryToHand };

            /// <summary>
            /// 결제/가격 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Payment = { ToolDefinitions.GetPaymentPriceList };

            /// <summary>
            /// 액션 정보 관련 도구들
            /// </summary>
            public static readonly ChatTool[] ActionInfo = { };

            /// <summary>
            /// 월드 정보 관련 도구들
            /// </summary>
            public static readonly ChatTool[] WorldInfo = { ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetCurrentTime, ToolDefinitions.FindBuildingAreaPath, ToolDefinitions.FindShortestAreaPathFromActor, ToolDefinitions.GetWorldAreaStructureText };

            /// <summary>
            /// 메모리 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Memory = { ToolDefinitions.GetUserMemory, ToolDefinitions.GetShortTermMemory, ToolDefinitions.GetLongTermMemory, ToolDefinitions.GetMemoryStats, ToolDefinitions.GetActorLocationMemories, ToolDefinitions.GetActorLocationMemoriesFiltered, ToolDefinitions.LoadRelationshipByName };

            /// <summary>
            /// 계획 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Plan = { ToolDefinitions.GetCurrentPlan, ToolDefinitions.GetCurrentSpecificAction };

            /// <summary>
            /// 모든 도구들
            /// </summary>
            public static readonly ChatTool[] All = { ToolDefinitions.SwapInventoryToHand, ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetUserMemory, ToolDefinitions.GetShortTermMemory, ToolDefinitions.GetLongTermMemory, ToolDefinitions.GetMemoryStats, ToolDefinitions.GetCurrentTime, ToolDefinitions.GetCurrentPlan, ToolDefinitions.GetCurrentSpecificAction, ToolDefinitions.FindBuildingAreaPath, ToolDefinitions.FindShortestAreaPathFromActor, ToolDefinitions.LoadRelationshipByName };
        }

        /// <summary>
        /// 도구 세트를 ChatCompletionOptions에 추가
        /// </summary>
        public static void AddToolsToOptions(ChatCompletionOptions options, params ChatTool[] tools)
        {
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }
        }

        /// <summary>
        /// 도구 세트를 ChatCompletionOptions에 추가 (세트 이름으로)
        /// </summary>
        public static void AddToolSetToOptions(ChatCompletionOptions options, ChatTool[] toolSet)
        {
            AddToolsToOptions(options, toolSet);
        }

        /// <summary>
        /// 공급자-중립 세트를 OpenAI 옵션에 추가 (점진적 마이그레이션용)
        /// </summary>
        public static void AddNeutralToolSetToOptions(ChatCompletionOptions options, LLMToolSchema[] toolSet)
        {
            AddToolsToOptionsFromSchemas(options, toolSet);
        }
    }

    /// <summary>
    /// 도구 실행을 위한 인터페이스
    /// </summary>
    public interface IToolExecutor
    {
        string ExecuteTool(ChatToolCall toolCall);
        public string ExecuteTool(GeminiFuncionCall functionCall);
    }

    /// <summary>
    /// 기본 도구 실행자 (Actor 기반)
    /// </summary>
    public class ToolExecutor : IToolExecutor
    {
        private readonly Actor actor;

        public ToolExecutor(Actor actor)
        {
            this.actor = actor;
        }

        public string ExecuteTool(ChatToolCall toolCall)
        {
            return ExecuteTool(toolCall.FunctionName, toolCall.FunctionArguments);
        }

        public string ExecuteTool(GeminiFuncionCall functionCall)
        {
            string result = null;
            // 1. Args가 JsonElement 타입인지 확인하고 안전하게 변환합니다.
            if (functionCall.Args is JsonElement argsElement)
            {
                // 2. JsonElement를 JSON 문자열로 변환합니다.
                string jsonString = argsElement.GetRawText();

                // 3. JSON 문자열로부터 BinaryData 객체를 생성합니다.
                BinaryData argsAsBinaryData = BinaryData.FromString(jsonString);

                // 4. 올바르게 생성된 BinaryData를 함수에 전달합니다.
                result = ExecuteTool(functionCall.Name, argsAsBinaryData);
            }
            return result;
        }

        public string ExecuteTool(string toolName, System.BinaryData arguments)
        {
            switch (toolName)
            {
                case nameof(SwapInventoryToHand):
                    return SwapInventoryToHand(arguments);
                case nameof(GetPaymentPriceList):
                    return GetPaymentPriceList();
                case nameof(GetWorldAreaInfo):
                    return GetWorldAreaInfo();
                case nameof(FindBuildingAreaPath):
                    return FindBuildingAreaPath(arguments);
                case nameof(FindShortestAreaPathFromActor):
                    return FindShortestAreaPathFromActor(arguments);
                case nameof(GetWorldAreaStructureText):
                    return GetWorldAreaStructureText();
                case nameof(GetActorLocationMemories):
                    return GetActorLocationMemories();
                case nameof(GetActorLocationMemoriesFiltered):
                    return GetActorLocationMemoriesFiltered(arguments);
                case nameof(GetUserMemory):
                    return GetUserMemory();
                case nameof(GetShortTermMemory):
                    return GetShortTermMemory(arguments);
                case nameof(GetLongTermMemory):
                    return GetLongTermMemory(arguments);
                case nameof(GetMemoryStats):
                    return GetMemoryStats();
                case nameof(GetCurrentTime):
                    return GetCurrentTime();
                case nameof(GetCurrentPlan):
                    return GetCurrentPlan();
                case nameof(GetCurrentSpecificAction):
                    return GetCurrentSpecificAction();
                case nameof(LoadRelationshipByName):
                    return LoadRelationshipByName(arguments);
                default:
                    return $"Error: Unknown tool '{toolName}'";
            }
        }

        private string SwapInventoryToHand(System.BinaryData arguments)
        {
            try
            {
                using var argumentsJson = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!argumentsJson.RootElement.TryGetProperty("itemName", out var itemNameElement))
                {
                    return "Error: itemName parameter is required";
                }

                string itemName = itemNameElement.GetString();
                if (string.IsNullOrEmpty(itemName))
                {
                    return "Error: Item name is required";
                }

                int targetSlot = -1;
                Item inventoryItem = null;

                // 아이템 이름으로 찾기
                for (int i = 0; i < actor.InventoryItems.Length; i++)
                {
                    if (actor.InventoryItems[i] != null && actor.InventoryItems[i].Name.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSlot = i;
                        inventoryItem = actor.InventoryItems[i];
                        break;
                    }
                }

                if (targetSlot == -1)
                {
                    var availableItems = new List<string>();
                    for (int i = 0; i < actor.InventoryItems.Length; i++)
                    {
                        if (actor.InventoryItems[i] != null)
                        {
                            availableItems.Add($"Slot {i}: {actor.InventoryItems[i].Name}");
                        }
                    }
                    return $"Error: Item '{itemName}' not found in inventory. Available items: {string.Join(", ", availableItems)}";
                }

                var currentHandItem = actor.HandItem;

                // 인벤토리 아이템을 핸드로 이동
                actor.InventoryItems[targetSlot] = currentHandItem;
                if (currentHandItem != null)
                {
                    currentHandItem.curLocation = actor.Inven;
                }

                // 핸드 아이템 설정
                actor.HandItem = inventoryItem;
                inventoryItem.curLocation = actor.Hand;
                inventoryItem.transform.localPosition = new Vector3(0, 0, 0);

                string result = $"Successfully swapped inventory slot {targetSlot} ({inventoryItem.Name}) to hand";
                if (currentHandItem != null)
                {
                    result += $". Previous hand item ({currentHandItem.Name}) moved to inventory slot {targetSlot}";
                }
                else
                {
                    result += ". Hand was empty before";
                }

                Debug.Log($"[GPTToolExecutor] {result}");
                return result;
            }
            catch (Exception ex)
            {
                string error = $"Error swapping inventory to hand: {ex.Message}";
                Debug.LogError($"[GPTToolExecutor] {error}");
                return error;
            }
        }

        private string GetPaymentPriceList()
        {
            try
            {
                if (actor == null)
                {
                    return "Error: No actor bound to executor";
                }

                // priceList 노출 메서드 탐색 (GetPriceList)
                var method = actor.GetType().GetMethod("GetPriceList", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return "No price list available for this actor (payment not supported).";
                }

                var listObj = method.Invoke(actor, null) as System.Collections.IEnumerable;
                if (listObj == null)
                {
                    return "{\"items\":[],\"count\":0}";
                }

                var items = new System.Text.StringBuilder();
                items.Append("{\"items\":[");
                int count = 0;
                foreach (var entry in listObj)
                {
                    if (entry == null) continue;
                    var entryType = entry.GetType();
                    var nameField = entryType.GetField("itemName") as object ?? entryType.GetProperty("itemName")?.GetGetMethod();
                    var priceField = entryType.GetField("price") as object ?? entryType.GetProperty("price")?.GetGetMethod();

                    string itemName = null;
                    int price = 0;

                    if (nameField is System.Reflection.FieldInfo nf)
                    {
                        itemName = nf.GetValue(entry) as string;
                    }
                    else if (nameField is System.Reflection.MethodInfo ng)
                    {
                        itemName = ng.Invoke(entry, null) as string;
                    }

                    if (priceField is System.Reflection.FieldInfo pf)
                    {
                        price = (int)(pf.GetValue(entry) ?? 0);
                    }
                    else if (priceField is System.Reflection.MethodInfo pg)
                    {
                        var val = pg.Invoke(entry, null);
                        price = val is int iv ? iv : 0;
                    }

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        if (count > 0) items.Append(",");
                        items.Append($"{{\"name\":\"{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(itemName)}\",\"price\":{price}}}");
                        count++;
                    }
                }
                items.Append($"],\"count\":{count}}}");
                return items.ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GPTToolExecutor] GetPaymentPriceList error: {ex.Message}");
                return $"Error getting price list: {ex.Message}";
            }
        }




        private string GetWorldAreaInfo()
        {
            try
            {
                var locationService = Services.Get<ILocationService>();
                return locationService.GetWorldAreaInfo();
            }
            catch (Exception ex)
            {
                return $"Error getting world area info: {ex.Message}";
            }
        }

        private string GetUserMemory()
        {
            try
            {
                // Brain의 MemoryManager를 통해 메모리 정보 가져오기
                if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
                {
                    var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory();
                    var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories();

                    var memorySummary = $"단기 메모리 ({shortTermMemories.Count}개):\n";
                    foreach (var memory in shortTermMemories)
                    {
                        memorySummary += $"- {memory.content}\n";
                    }

                    if (longTermMemories.Count > 0)
                    {
                        memorySummary += $"\n장기 메모리 ({longTermMemories.Count}개):\n";
                        foreach (var memory in longTermMemories)
                        {
                            memorySummary += $"- {memory.content}\n";
                        }
                    }

                    return memorySummary;
                }

                return "메모리 정보를 찾을 수 없습니다.";
            }
            catch (Exception ex)
            {
                return $"Error getting user memory: {ex.Message}";
            }
        }

        private string FindBuildingAreaPath(System.BinaryData arguments)
        {
            try
            {
                using var args = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!args.RootElement.TryGetProperty("buildingName", out var nameEl))
                    return "Error: buildingName parameter is required";
                var buildingName = nameEl.GetString();
                if (string.IsNullOrWhiteSpace(buildingName))
                    return "Error: buildingName is empty";

                var buildings = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
                if (buildings == null || buildings.Length == 0)
                    return "Error: No buildings found in scene";

                Building target = null;
                // 1) exact match by localized name
                foreach (var b in buildings)
                {
                    var locName = b.GetLocalizedName();
                    if (!string.IsNullOrEmpty(locName) && string.Equals(locName, buildingName, StringComparison.OrdinalIgnoreCase))
                    { target = b; break; }
                }
                // 2) fallback: contains match
                if (target == null)
                {
                    foreach (var b in buildings)
                    {
                        var locName = b.GetLocalizedName();
                        if (!string.IsNullOrEmpty(locName) && locName.IndexOf(buildingName, StringComparison.OrdinalIgnoreCase) >= 0)
                        { target = b; break; }
                    }
                }
                if (target == null)
                    return $"Error: Building '{buildingName}' not found";

                // Return area path only (exclude building level)
                var areaPath = target.curLocation != null ? target.curLocation.LocationToString() : null;
                if (string.IsNullOrEmpty(areaPath))
                    return "Error: Could not resolve building's area path";
                return areaPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GPTToolExecutor] FindBuildingAreaPath error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string FindShortestAreaPathFromActor(System.BinaryData arguments)
        {
            try
            {
                using var args = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!args.RootElement.TryGetProperty("targetAreaKey", out var keyEl))
                    return "Error: targetAreaKey parameter is required";
                var targetKey = keyEl.GetString();
                if (string.IsNullOrWhiteSpace(targetKey))
                    return "Error: targetAreaKey is empty";

                var locationService = Services.Get<ILocationService>();
                var pathService = Services.Get<IPathfindingService>();
                if (locationService == null || pathService == null)
                    return "Error: Required services not available";

                var startArea = locationService.GetArea(actor.curLocation);
                if (startArea == null)
                    return "Error: Actor's current area could not be determined";

                var path = pathService.FindPathToLocation(startArea, targetKey) ?? new System.Collections.Generic.List<string>();
                if (path.Count == 0)
                    return $"No path found from {startArea.locationName} to {targetKey}";

                return string.Join(" -> ", path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GPTToolExecutor] FindShortestAreaPathFromActor error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string GetWorldAreaStructureText()
        {
            try
            {
                var relPath = "Assets/11.GameDatas/tokyo_area_structure.txt";
                if (!System.IO.File.Exists(relPath))
                {
                    return "Error: tokyo_area_structure.txt not found. Please run the exporter first (Tools > Area > Export Tokyo Area Structure TXT).";
                }
                var txt = System.IO.File.ReadAllText(relPath, System.Text.Encoding.UTF8);
                return txt ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"Error reading world area structure text: {ex.Message}";
            }
        }

        private string GetActorLocationMemories()
        {
            try
            {
                if (actor == null) return "Error: No actor bound";
                var path = System.IO.Path.Combine(Application.dataPath, "11.GameDatas", "Character", actor.Name, "memory", "location", "location_memories.json");
                if (!System.IO.File.Exists(path)) return $"Error: location_memories.json not found for {actor.Name}";
                return System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return $"Error reading location memories: {ex.Message}";
            }
        }

        private string GetActorLocationMemoriesFiltered(System.BinaryData arguments)
        {
            try
            {
                if (actor == null) return "Error: No actor bound";
                using var args = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!args.RootElement.TryGetProperty("areaKey", out var keyEl))
                    return "Error: areaKey parameter is required";
                var areaKey = keyEl.GetString();
                if (string.IsNullOrWhiteSpace(areaKey)) return "Error: areaKey is empty";

                var filePath = System.IO.Path.Combine(Application.dataPath, "11.GameDatas", "Character", actor.Name, "memory", "location", "location_memories.json");
                if (!System.IO.File.Exists(filePath)) return $"Error: location_memories.json not found for {actor.Name}";
                var json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);

                // Parse to dictionary
                var all = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, Memory.LocationData>>(json) ?? new System.Collections.Generic.Dictionary<string, Memory.LocationData>();

                // Build filter predicate: exact match or prefix match by scope, also accept leaf-only forms
                bool Matches(string key)
                {
                    if (string.Equals(key, areaKey, System.StringComparison.Ordinal)) return true; // exact full-path match
                    if (key.StartsWith(areaKey + ":", System.StringComparison.Ordinal)) return true; // scope match by prefix
                    // leaf-only forms: if areaKey has no colon, match last segment equality
                    if (!areaKey.Contains(":"))
                    {
                        var parts = key.Split(':');
                        var leaf = parts.Length > 0 ? parts[parts.Length - 1] : key;
                        if (string.Equals(leaf, areaKey, System.StringComparison.Ordinal)) return true;
                    }
                    return false;
                }

                var filtered = new System.Collections.Generic.Dictionary<string, Memory.LocationData>();
                foreach (var kv in all)
                {
                    if (Matches(kv.Key)) filtered[kv.Key] = kv.Value;
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(filtered, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"Error filtering location memories: {ex.Message}";
            }
        }

        private string GetCurrentTime()
        {
            try
            {
                var timeService = Services.Get<ITimeService>();
                var currentTime = timeService.CurrentTime;
                return $"Current simulation time: {currentTime} (Year: {currentTime.year}, Month: {currentTime.month}, Day: {currentTime.day}, Hour: {currentTime.hour:D2}, Minute: {currentTime.minute:D2})";
            }
            catch (Exception ex)
            {
                return $"Error getting current time: {ex.Message}";
            }
        }

        private string GetCurrentPlan()
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor))
                {
                    return "No plan available (not MainActor)";
                }

                // DayPlanner를 통해 현재 계획 정보 조회
                var dayPlanner = mainActor.brain.dayPlanner;
                if (dayPlanner == null)
                {
                    return "No plan available (DayPlanner not found)";
                }

                var currentPlan = dayPlanner.GetCurrentDayPlan();
                if (currentPlan == null)
                {
                    return "No current plan available";
                }

                return currentPlan.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GPTToolExecutor] GetCurrentPlan error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string GetCurrentSpecificAction()
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor))
                {
                    return "No specific action available (not MainActor)";
                }

                // DayPlanner를 통해 현재 특정 행동 조회
                var dayPlanner = mainActor.brain.dayPlanner;
                if (dayPlanner == null)
                {
                    return "No specific action available (DayPlanner not found)";
                }

                // 현재 특정 행동 가져오기 (동기적으로 처리)
                var currentSpecificAction = dayPlanner.GetCurrentSpecificActionAsync().GetAwaiter().GetResult();
                if (currentSpecificAction == null)
                {
                    return "No current specific action available";
                }

                // 특정 행동 정보 포맷팅
                var actionInfo = new List<string>();
                actionInfo.Add($"Action Type: {currentSpecificAction.ActionType}");
                actionInfo.Add($"Description: {currentSpecificAction.Description}");
                actionInfo.Add($"Duration: {currentSpecificAction.DurationMinutes} minutes");

                if (currentSpecificAction.ParentDetailedActivity != null)
                {
                    actionInfo.Add($"Activity: {currentSpecificAction.ParentDetailedActivity.ActivityName}");

                    if (currentSpecificAction.ParentDetailedActivity.ParentHighLevelTask != null)
                    {
                        actionInfo.Add($"Task: {currentSpecificAction.ParentDetailedActivity.ParentHighLevelTask.TaskName}");
                    }
                }

                return string.Join("\n", actionInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GPTToolExecutor] GetCurrentSpecificAction error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string LoadRelationshipByName(System.BinaryData arguments)
        {
            try
            {
                if (actor == null) return "Error: No actor bound";
                using var args = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!args.RootElement.TryGetProperty("targetName", out var nameEl))
                    return "Error: targetName parameter is required";
                var targetName = nameEl.GetString();
                if (string.IsNullOrWhiteSpace(targetName))
                    return "Error: targetName is empty";

                // Actor의 LoadRelationships(targetName) 사용
                return actor.LoadRelationships(targetName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GPTToolExecutor] LoadRelationshipByName error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string GetShortTermMemory(System.BinaryData arguments)
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
                {
                    return "No short-term memory available (not MainActor or no memory manager)";
                }

                // 파라미터 파싱
                string memoryType = "";
                int limit = 20;
                string keyword = "";

                if (arguments != null)
                {
                    using var argumentsJson = System.Text.Json.JsonDocument.Parse(arguments.ToString());

                    if (argumentsJson.RootElement.TryGetProperty("memoryType", out var memoryTypeElement))
                    {
                        memoryType = memoryTypeElement.GetString() ?? "";
                    }

                    if (argumentsJson.RootElement.TryGetProperty("limit", out var limitElement))
                    {
                        limit = Math.Min(limitElement.GetInt32(), 50);
                    }

                    if (argumentsJson.RootElement.TryGetProperty("keyword", out var keywordElement))
                    {
                        keyword = keywordElement.GetString() ?? "";
                    }
                }

                var memories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();

                // 필터링
                var filteredMemories = memories.AsEnumerable();

                if (!string.IsNullOrEmpty(memoryType))
                {
                    filteredMemories = filteredMemories.Where(m => m.type.Equals(memoryType, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(keyword))
                {
                    filteredMemories = filteredMemories.Where(m => m.content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                // 최신 순으로 정렬하고 제한
                var resultMemories = filteredMemories
                    .OrderByDescending(m => m.timestamp)
                    .Take(limit)
                    .ToList();

                if (resultMemories.Count == 0)
                {
                    return "No matching short-term memories found.";
                }

                var memoryTexts = resultMemories.Select(m =>
                    $"[{m.timestamp:yyyy-MM-dd HH:mm}] ({m.type}) {m.content}");

                return $"Short-term memories ({resultMemories.Count} found):\n\n{string.Join("\n", memoryTexts)}";
            }
            catch (Exception ex)
            {
                return $"Error getting short-term memory: {ex.Message}";
            }
        }

        private string GetLongTermMemory(System.BinaryData arguments)
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
                {
                    return "No long-term memory available (not MainActor or no memory manager)";
                }

                // 파라미터 파싱
                string searchQuery = "";
                string dateRange = "all";
                int limit = 10;

                if (arguments != null)
                {
                    using var argumentsJson = System.Text.Json.JsonDocument.Parse(arguments.ToString());

                    if (argumentsJson.RootElement.TryGetProperty("searchQuery", out var searchElement))
                    {
                        searchQuery = searchElement.GetString() ?? "";
                    }

                    if (argumentsJson.RootElement.TryGetProperty("dateRange", out var dateElement))
                    {
                        dateRange = dateElement.GetString() ?? "all";
                    }

                    if (argumentsJson.RootElement.TryGetProperty("limit", out var limitElement))
                    {
                        limit = Math.Min(limitElement.GetInt32(), 30);
                    }
                }

                var memories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<LongTermMemory>();

                // 검색 쿼리 필터링
                var filteredMemories = memories.AsEnumerable();

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    filteredMemories = filteredMemories.Where(m =>
                    {
                        var content = m.content ?? "";
                        return content.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
                    });
                }

                // 날짜 범위 필터링 (추후 구현 가능)
                // dateRange에 따른 필터링 로직은 필요시 추가

                // 최신 순으로 정렬하고 제한
                var resultMemories = filteredMemories.Take(limit).ToList();

                if (resultMemories.Count == 0)
                {
                    return "No matching long-term memories found.";
                }

                var memoryTexts = resultMemories.Select(m =>
                {
                    var date = m.timestamp.ToString();
                    var content = m.content ?? "No content";

                    return $"[{date}] {content}";
                });

                return $"Long-term memories ({resultMemories.Count} found):\n\n{string.Join("\n\n", memoryTexts)}";
            }
            catch (Exception ex)
            {
                return $"Error getting long-term memory: {ex.Message}";
            }
        }

        private string GetMemoryStats()
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
                {
                    return "No memory statistics available (not MainActor or no memory manager)";
                }

                var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
                var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<LongTermMemory>();

                // 단기 기억 통계
                var stmStats = new Dictionary<string, int>();
                foreach (var memory in shortTermMemories)
                {
                    stmStats[memory.type] = stmStats.GetValueOrDefault(memory.type, 0) + 1;
                }

                // 최근 활동 (최근 10개)
                var recentActivities = shortTermMemories
                    .OrderByDescending(m => m.timestamp)
                    .Take(10)
                    .Select(m => $"[{m.timestamp:HH:mm}] {m.type}")
                    .ToList();

                var statsText = new List<string>
                {
                    $"=== Memory Statistics for {actor.Name} ===",
                    "",
                    $"Short-term memories: {shortTermMemories.Count}",
                    $"Long-term memories: {longTermMemories.Count}",
                    "",
                    "Short-term memory breakdown:"
                };

                foreach (var stat in stmStats.OrderByDescending(kvp => kvp.Value))
                {
                    statsText.Add($"  - {stat.Key}: {stat.Value}");
                }

                if (recentActivities.Count > 0)
                {
                    statsText.Add("");
                    statsText.Add("Recent activity:");
                    statsText.AddRange(recentActivities.Select(a => $"  {a}"));
                }

                return string.Join("\n", statsText);
            }
            catch (Exception ex)
            {
                return $"Error getting memory statistics: {ex.Message}";
            }
        }
    }
}