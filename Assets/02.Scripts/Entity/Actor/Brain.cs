using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agent;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using PlanStructures;
using OpenAI.Chat;

/// <summary>
/// Actor의 인지 시스템을 담당하는 조정자 클래스
/// 
/// 책임:
/// - DayPlanner, Thinker, ActionPerformer 간의 조정
/// - Think/Act 프로세스의 핵심 로직
/// - 메모리 관리 및 상황 인식
/// 
/// 리팩토링 내역:
/// - DayPlan 관련 기능 → DayPlanner 클래스로 분리
/// - Think/Act 루프 관리 → Thinker 클래스로 분리  
/// - 액션 실행 → ActionPerformer 클래스로 분리
/// - Brain은 조정자 역할로 단순화
/// 
/// 사용 예시:
/// ```csharp
/// var brain = new Brain(actor);
/// await brain.StartDayPlanAndThink();
/// ```
/// </summary>
public class Brain
{
    public Actor actor;

    // --- Enhanced Memory System ---
    public MemoryManager memoryManager;

    // --- AI Agent Components ---
    //private ActSelectorAgent actSelectorAgent;
    //private PerceptionAgent perceptionAgent;
    //private ReactionDecisionAgent reactionDecisionAgent; // 외부 이벤트 반응 결정 Agent
    //private GPT gpt;

    // --- Refactored Components ---
    public DayPlanner dayPlanner;
    private Thinker thinker;
    private ActionPerformer actionPerformer;
    //private UseActionManager useActionManager; // Use Action 전용 매니저 추가

    /// <summary>
    /// Thinker 컴포넌트에 대한 외부 접근을 위한 프로퍼티
    /// </summary>
    public Thinker Thinker => thinker;

    public PerceptionResult recentPerceptionResult;

    // Perception 최초 캐시 체크 플래그를 Brain 단에서 관리
    public bool HasCheckedPerceptionCacheOnce = false;

    // --- Test Settings ---
    private bool forceNewDayPlan = false; // 기존 계획 무시하고 새로 생성 (테스트용)
    private bool planOnly = false; // 첫 계획 생성까지만 실행하고 그 이후에는 멈춤 (테스트용)

    [HideInInspector] public bool havePlan = false; // 계획 생성을 플러그로 처리하고 싶을 때

    [HideInInspector] public bool firstThink = true; // 첫 번째 Think 여부

    public Brain(Actor actor)
    {
        this.actor = actor;

        // Enhanced Memory System 초기화
        memoryManager = new MemoryManager(actor);

        // 리팩토링된 컴포넌트들 초기화
        dayPlanner = new DayPlanner(actor);
        thinker = new Thinker(actor, this);
        actionPerformer = new ActionPerformer(actor);
    }

    /// <summary>
    /// 강제로 새로운 DayPlan을 생성하도록 설정합니다.
    /// </summary>
    public void SetForceNewDayPlan(bool force)
    {
        forceNewDayPlan = force;
        //Debug.Log($"[{actor.Name}] Force new day plan {(force ? "enabled" : "disabled")}");
        // DayPlanner에도 전달하여 로드 분기를 우회하도록 동기화
        try
        {
            dayPlanner?.SetForceNewDayPlan(force);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{actor.Name}] Failed to set forceNewDayPlan on DayPlanner: {ex.Message}");
        }
    }

    /// <summary>
    /// 첫 계획 생성까지만 실행하도록 설정합니다.
    /// </summary>
    public void SetPlanOnly(bool only)
    {
        planOnly = only;
        Debug.Log($"[{actor.Name}] Plan only mode {(only ? "enabled" : "disabled")}");
    }



    /// <summary>
    /// Think/Act 루프를 백그라운드에서 시작합니다.
    /// </summary>
    public void StartThinkLoop()
    {
        if (planOnly)
        {
            Debug.Log($"[{actor.Name}] Plan only mode - Think 루프 시작하지 않음");
            return;
        }

        _ = thinker.StartThinkAndActLoop();
    }



    /// <summary>
    /// Perception 이전에 goal 업데이트가 필요한지 확인하고 한 번만 변경합니다.
    /// </summary>
    public async UniTask UpdateGoalBeforePerceptionAsync()
    {
        try
        {
            if (actor is not MainActor mainActor) return;
            if (mainActor.goalAlreadyChanged) return;

            // 설정이 없으면 스킵
            var targetAfter = mainActor.afterGoal;
            if (string.IsNullOrWhiteSpace(targetAfter)) return;

            // 에이전트에게 변경 필요 여부 질의
            var agent = new GoalUpdateAgent(actor);
            var decision = await agent.DecideAsync();
            if (decision == null || !decision.ShouldChange) return;

            // CharacterInfo 수정 및 저장
            var characterMemoryManager = new CharacterMemoryManager(actor);
            var info = characterMemoryManager.GetCharacterInfo();

            info.Goal = targetAfter.Trim();
            await characterMemoryManager.SaveCharacterInfoAsync();

            // 1회 플래그 세팅
            mainActor.goalAlreadyChanged = true;
            Debug.Log($"[{actor.Name}] goal 변경 완료 -> '{info.Goal}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Goal 사전 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 외부 이벤트가 발생했을 때 호출됩니다.
    /// PerceptionAgent를 실행하고 반응 여부를 결정한 후 적절한 조치를 취합니다.
    /// </summary>
    public void OnExternalEvent(string description = null)
    {
        try
        {
            Debug.Log($"[{actor.Name}] 외부 이벤트 발생 - 반응 여부 결정 시작");
            if (!string.IsNullOrEmpty(description))
            {
                try
                {
                    memoryManager.AddShortTermMemory("주변에서 변화가 감지되었다.", description, actor?.curLocation?.GetSimpleKey());
                }
                catch { }
            }
            thinker.OnExternalEventAsync();

        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 외부 이벤트 처리 실패: {ex.Message}");
            // 오류 발생 시 기본적으로 반응하지 않음
        }
    }

    public async UniTask<PerceptionResult> Perception()
    {
        try
        {
            // GPT 사용이 비활성화된 경우 기본 Wait 액션 반환
            if (!actor.UseGPT)
            {
                Debug.Log($"[{actor.Name}] GPT 비활성화됨 - Think 프로세스 건너뜀, Wait 액션 반환");

                // 기본 PerceptionResult 생성
                var defaultPerceptionResult = new PerceptionResult
                {
                    situation_interpretation = "GPT 비활성화로 인한 기본 상황 해석",
                    thought_chain = new List<string> { "GPT 비활성화", "기본 대기 모드" }
                };

                return defaultPerceptionResult;
            }

            // PerceptionAgent를 통해 시각정보 해석
            var perceptionResult = await InterpretVisualInformationAsync();

            // Enhanced Memory System: Perception 결과를 Short Term Memory에 직접 추가

            // 위치명을 반드시 포함
            string locName = actor.curLocation.GetSimpleKey();

            memoryManager.AddShortTermMemory(
                $"{perceptionResult.situation_interpretation}",
                $"{string.Join(" -> ", perceptionResult.thought_chain)}",
                locName,
                perceptionResult.emotions
            );

            return perceptionResult;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Perception 실행 중 오류: {ex.Message}");
            throw;
        }

    }

    public async UniTask DayPlan()
    {
        if (!havePlan && Services.Get<IGameService>().IsDayPlannerEnabled())
        {
            Debug.Log($"[{actor.Name}] DayPlan 시작");
            await dayPlanner.PlanToday();

            // Enhanced Memory System: 계획 생성을 Short Term Memory에 직접 추가
            var dayPlan = dayPlanner.GetCurrentDayPlan();
            if (dayPlan?.HighLevelTasks != null && dayPlan.HighLevelTasks.Count > 0)
            {
                var planSummary = string.Join(", ", dayPlan.HighLevelTasks.ConvertAll(t => t.Description));
                memoryManager.AddShortTermMemory("오늘의 계획을 세웠다.", $"목록: {planSummary}", actor?.curLocation?.GetSimpleKey());
            }

            // planOnly가 true이면 계획 생성 후 종료
            if (planOnly)
            {
                Debug.Log($"[{actor.Name}] Plan only mode - 계획 생성 완료, Think 루프 시작하지 않음");
            }

            havePlan = true;
        }
    }

    public async UniTask UpdateRelationship(PerceptionResult perceptionResult)
    {
        if (firstThink)
        {
            firstThink = false;
        }
        else
        {
            // RelationshipAgent: 관계 수정 여부 결정 및 적용
            var relationshipMemoryManager = new RelationshipMemoryManager(actor);
            await relationshipMemoryManager.ProcessRelationshipUpdatesAsync(perceptionResult);

            // === 계획 유지/수정 결정 및 필요 시 재계획 (DayPlanner 내부로 캡슐화) ===
            if (Services.Get<IGameService>().IsDayPlannerEnabled())
            {
                await dayPlanner.DecideAndMaybeReplanAsync(perceptionResult);
            }
        }
    }

    /// <summary>
    /// Think 프로세스를 실행합니다.
    /// 현재 상황을 분석하고 다음 행동을 결정합니다.
    /// </summary>
    public async UniTask<(ActSelectorAgent.ActSelectionResult, ActParameterResult)> Think(PerceptionResult perceptionResult, ActSelectorAgent actSelectorAgent)
    {
        try
        {
            // 상황 설명 생성 (기존 방식과 PerceptionAgent 결과를 결합)
            //var enhancedDescription = $"{situationDescription}\n\n=== Perception Analysis ===\n{perceptionResult.situation_interpretation}\n\n생각 Chainn: {string.Join(" -> ", perceptionResult.thought_chain)}";

            // ActSelectorAgent를 통해 행동 선택 (Tool을 통해 동적으로 액션 정보 제공)
            // AI Agent 초기화
            
            actSelectorAgent.SetDayPlanner(dayPlanner); // DayPlanner 설정
            var selection = await actSelectorAgent.SelectActAsync(perceptionResult);

            // 기존 버블 팝업 제거 → SimulationController의 텍스트로 대체
            try
            {
                if (selection != null && SimulationController.Instance != null)
                {
                    string text = !string.IsNullOrEmpty(selection.Intention)
                        ? $"의도: {selection.Intention}"
                        : $"행동 선택: {selection.ActType}";
                    SimulationController.Instance.SetActorActivityText(actor.Name, text);
                }
            }
            catch { }

            // Enhanced Memory System: ActSelector 결과를 Short Term Memory에 직접 추가
            if (selection != null)
            {
                // memoryManager.AddShortTermMemory(
                //     $"{selection.ActType.ToKorean()}을(를) 하기로 결정했다.",
                //     $"의도: {selection.Intention}"
                // , actor?.curLocation?.GetSimpleKey());

                // memoryManager.AddShortTermMemory(
                //     $"{selection.Intention}",
                //     $"{selection.Reasoning}"
                // , actor?.curLocation?.GetSimpleKey());
            }

            // ActSelectResult를 ActorManager에 저장
            Services.Get<IActorService>().StoreActResult(actor, selection);

            // 선택된 행동에 대한 파라미터 생성
            var paramResult = await GenerateActionParameters(selection, actSelectorAgent);

            return (selection, paramResult);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Think 프로세스 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 선택된 행동을 실행합니다.
    /// </summary>
    public async UniTask Act(ActParameterResult paramResult, CancellationToken token)
    {
        try
        {
            // Enhanced Memory System: 행동 시작을 Short Term Memory에 직접 기록
            if (paramResult.ActType != ActionType.End)
            {
                var startContent = !string.IsNullOrEmpty(paramResult.StartMemoryContent)
                    ? paramResult.StartMemoryContent
                    : $"{paramResult.ActType.ToKorean()}을(를) 시작했다.";
                memoryManager.AddShortTermMemory(
                    startContent,
                    "",
                    actor?.curLocation?.GetSimpleKey()
                );
            }

            // AgentAction으로 변환
            var action = new AgentAction
            {
                ActionType = paramResult.ActType,
                Parameters = paramResult.Parameters
            };

            // ActionPerformer를 통해 액션 실행
            bool isSuccess = true;
            //string completionReason = "성공적으로 완료";

            MainActor mainActor = actor as MainActor;
            try
            {

                mainActor.CurrentActivity = paramResult.ActType.ToString();
                isSuccess = await actionPerformer.ExecuteAction(action, token);
            }
            catch (OperationCanceledException)
            {
                isSuccess = false;
                throw;
            }
            catch (Exception actionEx)
            {
                isSuccess = false;
                Debug.LogError($"[{actor.Name}] 액션 실행 중 오류: {actionEx.Message}");
                mainActor.CurrentActivity = "Idle";
                throw;
            }
            finally
            {
                // 성공 STM은 각 핸들러에서 컨텍스트에 맞게 기록한다.
                mainActor.CurrentActivity = "Idle";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Act 프로세스 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 선택된 행동에 대한 파라미터를 생성합니다.
    /// </summary>
    private async UniTask<ActParameterResult> GenerateActionParameters(ActSelectorAgent.ActSelectionResult selection, ActSelectorAgent actSelectorAgent)
    {
        // UseGPT가 false이고 MainActor인 경우 Inspector 값 사용
        if (!actor.UseGPT && actor is MainActor mainActor)
        {
            return GetParametersFromInspector(mainActor, selection.ActType);
        }

        // Use Action은 UseActionManager를 통해 처리
        if (selection.ActType == ActionType.UseObject)
        {
            var request = new ActParameterRequest
            {
                Reasoning = selection.Reasoning,
                Intention = selection.Intention,
                ActType = selection.ActType
            };
            var useActionManager = new UseActionManager(actor);
            var useResult = await useActionManager.ExecuteUseActionAsync(request);
            if (useResult != null && string.IsNullOrEmpty(useResult.StartMemoryContent))
            {
                useResult.StartMemoryContent = BuildStartMemoryContent(selection.ActType, useResult.Parameters);
            }
            return useResult;
        }

        if (selection.ActType == ActionType.RemoveClothing)
        {
            return new ActParameterResult
            {
                ActType = selection.ActType,
                Parameters = new Dictionary<string, object>(),
                StartMemoryContent = BuildStartMemoryContent(selection.ActType, null)
            };
        }

        // 매개변수가 필요 없는 액션들은 바로 반환
        if (selection.ActType == ActionType.Wait || selection.ActType == ActionType.End)
        {
            return new ActParameterResult
            {
                ActType = selection.ActType,
                Parameters = new Dictionary<string, object>(),
                StartMemoryContent = BuildStartMemoryContent(selection.ActType, null)
            };
        }

        // 다른 액션들은 필요할 때마다 ParameterAgent를 생성하여 처리
        var parameterAgent = ParameterAgentFactory.CreateParameterAgent(selection.ActType, actor);
        if (parameterAgent != null)
        {
            var request = new ActParameterRequest
            {
                Reasoning = selection.Reasoning,
                Intention = selection.Intention,
                ActType = selection.ActType
            };
            var result = await parameterAgent.GenerateParametersAsync(request);
            if (result == null)
            {
                Debug.LogWarning($"[{actor.Name}] 파라미터 생성이 null로 반환됨 - 액션 취소 후 재선택으로 회귀");
                // 재선택: 최신 Perception 유지하여 다시 SelectAct 수행
                // var selectionRetry = new ActSelectorAgent(actor);
                // selectionRetry.SetDayPlanner(dayPlanner);
                actSelectorAgent.AddUserMessage("잘못된 행동 선택, 다시 선택해주세요. 행동에 대한 대상과 충분히 가까운지, 존재하는지 확인해주세요.");
                var selectionRetryResult = await actSelectorAgent.SelectActAsync(recentPerceptionResult);
                return await GenerateActionParameters(selectionRetryResult, actSelectorAgent);
            }
            if (string.IsNullOrEmpty(result.StartMemoryContent))
            {
                result.StartMemoryContent = BuildStartMemoryContent(selection.ActType, result.Parameters);
            }
            return result;
        }
        else
        {
            // Wait, Use 등 매개변수가 필요 없는 액션들
            Debug.Log($"[{actor.Name}] {selection.ActType} 액션은 매개변수가 필요 없음 - 바로 실행");
            var simpleResult = new ActParameterResult
            {
                ActType = selection.ActType,
                Parameters = new Dictionary<string, object>()
            };
            simpleResult.StartMemoryContent = BuildStartMemoryContent(selection.ActType, simpleResult.Parameters);
            return simpleResult;
        }
    }

    /// <summary>
    /// 액션 시작 시 STM에 기록할 자연어 문장을 생성합니다.
    /// </summary>
    private string BuildStartMemoryContent(ActionType actionType, Dictionary<string, object> parameters)
    {
        try
        {
            switch (actionType)
            {
                case ActionType.MoveToArea:
                    if (parameters != null && parameters.TryGetValue("area_name", out var areaObj))
                    {
                        var area = areaObj?.ToString();
                        if (!string.IsNullOrEmpty(area)) return $"'{area}'으로 이동하기로 했다.";
                    }
                    return "다른 곳으로 이동하기로 했다.";

                case ActionType.MoveToEntity:
                    if (parameters != null && parameters.TryGetValue("entity_name", out var entObj))
                    {
                        var entity = entObj?.ToString();
                        if (!string.IsNullOrEmpty(entity)) return $"'{entity}' 쪽으로 다가가기로 했다.";
                    }
                    return "누군가/무언가에게 다가가기로 했다.";

                case ActionType.Talk:
                    {
                        var who = parameters != null && parameters.TryGetValue("character_name", out var whoObj) ? whoObj?.ToString() : null;
                        var msg = parameters != null && parameters.TryGetValue("message", out var msgObj) ? msgObj?.ToString() : null;
                        //if (!string.IsNullOrEmpty(who) && !string.IsNullOrEmpty(msg)) return $"'{who}'에게 이렇게 말하기로 했다: '{msg}'.";
                        if (!string.IsNullOrEmpty(who)) return $"'{who}'에게 말을 하기로 했다.";
                        return "말을 하기로 했다.";
                    }

                case ActionType.PickUpItem:
                    {
                        var item = parameters != null && (parameters.TryGetValue("item_name", out var nameObj) || parameters.TryGetValue("target_item", out nameObj)) ? nameObj?.ToString() : null;
                        if (!string.IsNullOrEmpty(item)) return $"'{item}'을(를) 집기로 했다.";
                        return "눈앞의 물건을 집기로 했다.";
                    }

                case ActionType.InteractWithObject:
                    if (parameters != null && parameters.TryGetValue("object_name", out var objObj))
                    {
                        var obj = objObj?.ToString();
                        if (!string.IsNullOrEmpty(obj)) return $"'{obj}'과(와) 상호작용하기로 했다.";
                    }
                    return "주변 물건과 상호작용하기로 했다.";

                case ActionType.PutDown:
                    {
                        var targetKey = parameters != null && parameters.TryGetValue("target_key", out var tk) ? tk?.ToString() : null;
                        var heldName = (actor as MainActor)?.HandItem?.Name ?? "물건";
                        if (!string.IsNullOrEmpty(targetKey)) return $"'{heldName}'을(를) '{targetKey}'에 내려놓기로 했다.";
                        return $"'{heldName}'을(를) 내려놓기로 했다.";
                    }

                case ActionType.GiveMoney:
                    {
                        var who = parameters != null && parameters.TryGetValue("target_character", out var to) ? to?.ToString() : null;
                        var amount = parameters != null && parameters.TryGetValue("amount", out var a) ? a?.ToString() : null;
                        if (!string.IsNullOrEmpty(who) && !string.IsNullOrEmpty(amount)) return $"'{who}'에게 {amount}원을 건네기로 했다.";
                        return "돈을 건네기로 했다.";
                    }

                case ActionType.GiveItem:
                    {
                        var who = parameters != null && parameters.TryGetValue("target_character", out var to) ? to?.ToString() : null;
                        var heldName = (actor as MainActor)?.HandItem?.Name ?? "물건";
                        if (!string.IsNullOrEmpty(who)) return $"'{who}'에게 '{heldName}'을(를) 건네기로 했다.";
                        return $"'{heldName}'을(를) 건네기로 했다.";
                    }

                case ActionType.PerformActivity:
                    {
                        var name = parameters != null && parameters.TryGetValue("activity_name", out var an) ? an?.ToString() : null;
                        var duration = parameters != null && parameters.TryGetValue("duration", out var du) ? du?.ToString() : null;
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(duration)) return $"'{name}'을(를) 하기로 했다. (약 {duration}분)";
                        if (!string.IsNullOrEmpty(name)) return $"'{name}'을(를) 하기로 했다.";
                        return "어떤 활동을 하기로 했다.";
                    }

                case ActionType.UseObject:
                    {
                        var hand = (actor as MainActor)?.HandItem;
                        if (hand is iPhone)
                        {
                            var cmd = parameters != null && parameters.TryGetValue("command", out var c) ? c?.ToString() : null;
                            var target = parameters != null && parameters.TryGetValue("target_actor", out var ta) ? ta?.ToString() : null;
                            if (cmd == "chat" && !string.IsNullOrEmpty(target) && parameters.TryGetValue("message", out var msg))
                                return $"아이폰으로 '{target}'에게 메시지를 보내기로 했다: '{msg}'.";
                            if (cmd == "recent_read" && !string.IsNullOrEmpty(target) && parameters.TryGetValue("message_count", out var mc))
                                return $"아이폰에서 '{target}'와의 최근 대화를 {mc}개 읽어보기로 했다.";
                            if (cmd == "continue_read" && !string.IsNullOrEmpty(target) && parameters.TryGetValue("message_count", out var cc))
                                return $"아이폰에서 '{target}'와의 지난 대화를 더 읽어보기로 했다 ({cc}).";
                            return "아이폰을 사용해 보기로 했다.";
                        }
                        if (hand is Note)
                        {
                            var action = parameters != null && parameters.TryGetValue("action", out var a) ? a?.ToString() : null;
                            if (action == "write") return "노트에 글을 적어보기로 했다.";
                            if (action == "read") return "노트를 읽어보기로 했다.";
                            if (action == "rewrite") return "노트의 글을 고쳐 쓰기로 했다.";
                            if (action == "erase") return "노트에서 일부를 지우기로 했다.";
                            return "노트를 사용해 보기로 했다.";
                        }
                        if (hand is Book)
                        {
                            int page = 1;
                            if (parameters != null)
                            {
                                if (parameters.TryGetValue("page_number", out var pn) && pn is int i1) page = i1;
                                else if (parameters.TryGetValue("page", out var p) && p is int i2) page = i2;
                            }
                            return $"책을 읽어보기로 했다. {page}쪽부터.";
                        }
                        var name = hand?.Name ?? "물건";
                        return $"'{name}'을(를) 사용해 보기로 했다.";
                    }

                case ActionType.Wait:
                    return "잠시 생각을 정리하기로 했다.";

                case ActionType.RemoveClothing:
                    return "입은 옷을 벗기로 했다.";

                case ActionType.End:
                    return "...";

                case ActionType.Sleep:
                    return "잠자리에 들기로 했다.";

                case ActionType.Cook:
                    {
                        var key = parameters != null && parameters.TryGetValue("target_key", out var tk) ? tk?.ToString() : null;
                        if (!string.IsNullOrEmpty(key)) return $"'{key}'를 만들기로 했다.";
                        return "무언가를 만들어 보기로 했다.";
                    }
            }
        }
        catch { }
        return $"{actionType.ToKorean()}을(를) 시작했다.";
    }

    /// <summary>
    /// MainActor의 Inspector에서 파라미터를 가져옵니다.
    /// </summary>
    private ActParameterResult GetParametersFromInspector(MainActor mainActor, ActionType actionType)
    {
        // ManualActionController의 debugActionParameters에 접근
        var manualController = GetManualActionController(mainActor);
        if (manualController == null)
        {
            Debug.LogWarning($"[{mainActor.Name}] ManualActionController를 찾을 수 없음");
            return new ActParameterResult
            {
                ActType = actionType,
                Parameters = new Dictionary<string, object>()
            };
        }

        var parameters = GetInspectorParameters(manualController);

        Debug.Log($"[{mainActor.Name}] Inspector에서 파라미터 사용: {actionType} with parameters: [{string.Join(", ", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]");

        return new ActParameterResult
        {
            ActType = actionType,
            Parameters = parameters
        };
    }

    /// <summary>
    /// MainActor에서 ManualActionController를 가져옵니다.
    /// </summary>
    private ManualActionController GetManualActionController(MainActor mainActor)
    {
        // Reflection을 사용하여 private field에 접근
        var field = typeof(MainActor).GetField("manualActionController",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(mainActor) as ManualActionController;
    }

    /// <summary>
    /// ManualActionController에서 파라미터를 가져옵니다.
    /// </summary>
    private Dictionary<string, object> GetInspectorParameters(ManualActionController controller)
    {
        return controller.GetCurrentParameters();
    }

    /// <summary>
    /// PerceptionAgent를 통해 시각정보를 해석합니다.
    /// </summary>
    public async UniTask<PerceptionResult> InterpretVisualInformationAsync()
    {
        if (actor is MainActor mainActor)
        {
            var visualInformation = mainActor.sensor.GetLookableEntityDescriptions();
            var perceptionAgent = new PerceptionAgentGroup(actor, dayPlanner);
            recentPerceptionResult = await perceptionAgent.InterpretVisualInformationAsync(visualInformation);

            CharacterMemoryManager characterMemoryManager = new CharacterMemoryManager(actor);
            var characterInfo = characterMemoryManager.GetCharacterInfo();
            characterInfo.SetEmotions(recentPerceptionResult.emotions);
            await characterMemoryManager.SaveCharacterInfoAsync();

            return recentPerceptionResult;
        }

        Debug.LogError($"[{actor.Name}] MainActor가 아닌 Actor에서 시각정보 해석을 시도했습니다.");
        throw new System.InvalidOperationException($"{actor.Name}은 MainActor가 아니므로 시각정보 해석을 수행할 수 없습니다.");
    }   /// <summary>
        /// 로깅 활성화 여부를 설정합니다.
        /// </summary>
    public void SetLoggingEnabled(bool enabled)
    {
        // GPT 로깅 설정
        // if (gpt != null)
        // {
        //     // GPT 클래스에 로깅 설정 메서드가 있다면 호출
        // }
    }

    /// <summary>
    /// 하루가 끝날 때 Long Term Memory 통합 프로세스를 실행합니다.
    /// </summary>
    public async UniTask ProcessDayEndMemoryAsync()
    {
        try
        {
            Debug.Log($"[{actor.Name}] 하루 종료 - Long Term Memory 통합 시작");

            await memoryManager.ProcessDayEndMemoryAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Day End Memory 처리 실패: {ex.Message}");
        }
    }

    public async UniTask ProcessCircleEndMemoryAsync()
    {
        try
        {
            Debug.Log($"[{actor.Name}] 사이클 종료 - Long Term Memory 통합 시작");

            // STM 개수 확인 후 임계치(12) 초과일 때만 처리
            int stmCount = memoryManager != null ? memoryManager.GetShortTermMemoryCount() : 0;
            const int StmKeepThreshold = 50; // MemoryManager의 보존 갯수와 일치
            if (stmCount <= StmKeepThreshold)
            {
                Debug.Log($"[{actor.Name}] STM {stmCount}개 → 보존 임계치 {StmKeepThreshold} 이하, 서클 정리 스킵");
                return;
            }

            await memoryManager.ProcessCircleEndMemoryAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Circle End Memory 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Long Term Memory 정기 정리를 실행합니다.
    /// </summary>
    public async UniTask PerformLongTermMemoryMaintenanceAsync()
    {
        try
        {
            Debug.Log($"[{actor.Name}] Long Term Memory 정기 정리 시작");

            // LTM 개수 임계치 확인 후에만 정리 실행
            int ltmCount = memoryManager != null ? memoryManager.GetLongTermMemories().Count : 0;
            const int LtmMaintenanceThreshold = 100; // 임계치. 필요시 조정 가능
            if (ltmCount <= LtmMaintenanceThreshold)
            {
                Debug.Log($"[{actor.Name}] LTM {ltmCount}개 → 임계치 {LtmMaintenanceThreshold} 이하, 정기 정리 스킵");
                return;
            }

            await memoryManager.PerformLongTermMemoryMaintenanceForCircleAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Long Term Memory 정리 실패: {ex.Message}");
        }
    }
}

