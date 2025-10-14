using System;
using System.Collections.Generic;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.Text.Json.Nodes;

public abstract class LLMClient
{
    public LLMClientProps llmOptions;
    protected string agentTypeOverride = "UNKNOWN";
    protected string actorName = "Unknown"; // Actor 이름을 저장할 변수
    protected IToolExecutor toolExecutor;
    protected Actor actor;
    private static readonly ConcurrentDictionary<string, object> ActorCacheLocks = new ConcurrentDictionary<string, object>(System.StringComparer.Ordinal);
    // 전역 직렬화 게이트: 모든 LLM 요청을 한 번에 하나씩만 처리 (FIFO by semaphore)
    private static readonly SemaphoreSlim GlobalSendGate = new SemaphoreSlim(1, 1);
    private static volatile bool SerializeAllRequests = true;
    // 현재 요청 컨텍스트의 메시지 해시를 보관하여 저장 시 재사용
    private string currentMsgHash;
    public LLMClient(LLMClientProps options)
    {
        this.llmOptions = options;
    }

    // JSON 설정: Enum을 문자열로 저장/읽기 (기존 숫자 값도 허용)
    private static readonly JsonSerializerSettings EnumAsStringJsonSettings = new JsonSerializerSettings
    {
        Converters = new List<JsonConverter>
        {
            new StringEnumConverter { AllowIntegerValues = true }
        }
    };

    protected void SetAgentType(string agentType)
    {
        agentTypeOverride = agentType;
    }

    /// <summary>
    /// Actor 이름 설정 (로깅용)
    /// </summary>
    protected void SetActor(Actor actor)
    {
        this.actor = actor;
        actorName = actor.Name;
        //Debug.Log($"[LLMClient] Actor name set to: {actorName}");
    }
    
    // 각 LLM 구현체에서 캐시 키 생성을 위해 필요한 객체를 반환합니다.
    protected virtual object GetHashKey()
    {
        var str = actor.LoadCharacterInfo() + actor.LoadCharacterMemory() + actor.LoadActorSituation();
        return str;
    }
    #region 메시지 관리
    protected abstract int GetMessageCount();
    protected abstract void RemoveAt(int index);
    protected abstract void RemoveMessage(AgentChatMessage message);
    protected abstract void ClearMessages(bool keepSystemMessage = false);
    public abstract void AddMessage(AgentChatMessage message);
    public abstract void AddSystemMessage(string message);
    public abstract void AddUserMessage(string message);
    public abstract void AddAssistantMessage(string message);
    public abstract void AddToolMessage(string name, string id, string message);
    #endregion

    #region Send Message
    public delegate T ChatDeserializer<T>(string response);

    public async UniTask<T> SendWithCacheLog<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null)
    {
        if (SerializeAllRequests)
        {
            await GlobalSendGate.WaitAsync();
        }
        try
        {
        #region 캐시 가능한 로그 기록 있는지 체크
        try
        {
            var cacheTimeService = Services.Get<ITimeService>();
            var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", "CachedLogs", actorName ?? "Unknown");
            if (cacheTimeService != null && Directory.Exists(baseDir))
            {
                //int count = actor.CacheCount;
                var agentPart = string.IsNullOrEmpty(agentTypeOverride) ? "UNKNOWN" : agentTypeOverride;
                var msgHash = ComputeMessagesHash(GetHashKey());
                currentMsgHash = msgHash; // 저장 시 재사용

                // 조회 전략: GameService에서 per-actor로 느슨/정밀 모드 선택
                bool useLoose = false;
                try
                {
                    var gameSvc = Services.Get<IGameService>();
                    if (gameSvc != null && actor != null)
                    {
                        useLoose = gameSvc.ShouldUseLooseCacheMatchFor(actor);
                    }
                }
                catch { }

                string pattern = useLoose
                    ? $"{actor.CacheCount}_*_{agentPart}_*.json"    // 느슨: msgHash 무시
                    : $"{actor.CacheCount}_*_{agentPart}_{msgHash}.json"; // 정밀: msgHash 포함

                // 캐시 조회: 설정된 패턴으로 탐색
                var exactMatch = Directory.GetFiles(baseDir, pattern);
                string matchPath = null;

                if (exactMatch != null && exactMatch.Length > 0)
                {
                    matchPath = exactMatch[0];
                }
                else
                {
                    // 정확 매치가 없으면 선 저장, 후 정리 전략으로 전환 (여기서는 삭제하지 않음)
                    Debug.LogWarning($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 정확 매치 없음(actor Cache Count = {actor.CacheCount})({matchPath}). 새 캐시 저장 후 동일 count 기존 캐시 정리 예정");
                }

                if (!string.IsNullOrEmpty(matchPath))
                {
                    // 느슨한 매칭 모드일 때, 발견된 파일의 파일명 해시를 현재 msgHash로 동기화
                    if (useLoose)
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(matchPath);
                            var fileName = Path.GetFileName(matchPath);
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                int lastUnderscore = fileName.LastIndexOf('_');
                                int dot = fileName.LastIndexOf('.')
;                                if (lastUnderscore > 0 && dot > lastUnderscore)
                                {
                                    var currentSuffixHash = fileName.Substring(lastUnderscore + 1, dot - (lastUnderscore + 1));
                                    if (!string.Equals(currentSuffixHash, msgHash, StringComparison.OrdinalIgnoreCase))
                                    {
                                        var baseWithoutHash = fileName.Substring(0, lastUnderscore);
                                        var newFileName = baseWithoutHash + "_" + msgHash + ".json";
                                        var newPath = Path.Combine(dir ?? string.Empty, newFileName);
                                        if (!string.Equals(newPath, matchPath, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (!File.Exists(newPath))
                                            {
                                                File.Move(matchPath, newPath);
                                                matchPath = newPath;
                                            }
                                            else
                                            {
                                                // 대상 파일이 이미 존재하면, 기존 파일을 유지(이중 파일 방지)
                                                matchPath = newPath; // 새 경로를 우선 사용
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    var cachedJson = File.ReadAllText(matchPath);
                    try
                    {
                        // 1) 우선 캐시 엔벨로프 형식인지 점검: { payload: ..., tools: [...] }
                        var token = JToken.Parse(cachedJson);
                        if (token is JObject obj && obj["payload"] != null)
                        {
                            // 1-1) 도구 리플레이
                            try
                            {
                                var toolsToken = obj["tools"] as JArray;
                                if (toolsToken != null && toolsToken.Count > 0 && toolExecutor != null)
                                {
                                    foreach (var tkn in toolsToken)
                                    {
                                        try
                                        {
                                            var name = tkn["name"]?.ToString();
                                            var argsJson = tkn["argsJson"]?.ToString();
                                            if (!string.IsNullOrWhiteSpace(name))
                                            {
                                            JsonNode argsNode = null;
                                            try { if (!string.IsNullOrWhiteSpace(argsJson)) argsNode = JsonNode.Parse(argsJson); } catch { argsNode = null; }
                                            argsNode ??= JsonNode.Parse("{}");
                                            toolExecutor.ExecuteTool(name, argsNode);
                                            }
                                        }
                                        catch (Exception toolEx)
                                        {
                                            Debug.LogWarning($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 도구 리플레이 실패: {toolEx.Message}");
                                        }
                                    }
                                }
                            }
                            catch { }

                            // 1-2) payload를 T로 역직렬화하여 반환
                            var payloadJson = obj["payload"]?.ToString();
                            if (payloadJson != null)
                            {
                                var cachedPayload = JsonConvert.DeserializeObject<T>(payloadJson, EnumAsStringJsonSettings);
                                if (cachedPayload != null)
                                {
                                    Debug.Log($"<b><color=Yellow>[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 로그 히트(리플레이 포함): (actor Cache Count = {actor.CacheCount}) {matchPath}</color></b>");
                                    actor.CacheCount++; // 히트 시 증가
                                    return cachedPayload;
                                }
                            }
                        }
                        else
                        {
                            // 2) 구버전(엔벨로프 미사용) 캐시 호환: 직접 T로 역직렬화
                            var cached = JsonConvert.DeserializeObject<T>(cachedJson, EnumAsStringJsonSettings);
                            if (cached != null)
                            {
                                Debug.Log($"<b><color=Yellow>[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 로그 히트: {matchPath}</color></b>");
                                actor.CacheCount++; // 히트 시 증가
                                return cached;
                            }
                        }
                    }
                    catch (Newtonsoft.Json.JsonException ex)
                    {
                        Debug.LogError($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 타입 불일치 - JSON 역직렬화 실패: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 로그 확인 중 오류: {ex.Message}");
        }
        #endregion

        #region GPT API 호출 전 승인 요청
        // GPT API 호출 전 승인 요청
        var gameService = Services.Get<IGameService>();
        var approvalService = Services.Get<IGPTApprovalService>();

        if (gameService != null && gameService.IsGPTApprovalEnabled() && approvalService != null)
        {
            string agentType = agentTypeOverride;

            bool approved = await approvalService.RequestApprovalAsync(actorName, agentType);

            if (!approved)
            {
                Debug.LogError($"[{agentTypeOverride ?? "Unknown"}][{actorName}] GPT API 호출이 거부되었습니다: {agentType}");
                throw new OperationCanceledException($"GPT API 호출이 거부되었습니다: {actorName} - {agentType}");
            }

            Debug.Log($"[{agentTypeOverride ?? "Unknown"}][{actorName}] GPT API 호출이 승인되었습니다: {agentType}");
        }
        else if (gameService != null && !gameService.IsGPTApprovalEnabled())
        {
            Debug.Log($"[{agentTypeOverride ?? "Unknown"}][{actorName}] GPT 승인 시스템이 비활성화되어 자동으로 진행합니다: {agentTypeOverride}");
        }
        #endregion

        #region GPT API 호출 시 시간 정지
        // 승인 사용 중이면 시간 정지는 ApprovalService에서, 여기서는 느린 진행만 적용
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            // 모델 대기 동안 시뮬레이션 시간 완전 정지
            timeService.StartAPICall();
            Debug.Log($"[{agentTypeOverride ?? "Unknown"}][{actorName}] API 호출 시작 - 시뮬레이션 시간 정지됨");
        }
        #endregion

        #region GPT API 호출
        try
        {
            return await Send<T>();

        }
        #endregion
        finally
        {
            #region GPT API 호출 종료시 시간 재개
            if (timeService != null)
            {
                timeService.EndAPICall();
                Debug.Log($"[{agentTypeOverride ?? "Unknown"}][{actorName}] API 호출 종료 - 시뮬레이션 시간 재개됨");
            }
            #endregion
        }
        }
        finally
        {
            if (SerializeAllRequests)
            {
                try { GlobalSendGate.Release(); } catch { }
            }
        }
    }

    /// <summary>
    /// 지정된 count에 해당하는 현재 에이전트의 캐시 파일만 삭제합니다.
    /// </summary>
    private void DeleteCacheFilesFromCount(string baseDir, int startCount)
    {
        try
        {
            int deletedCount = 0;
            var files = Directory.GetFiles(baseDir, $"{startCount}_*.json");
            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                File.Delete(file);
                deletedCount++;
                Debug.LogWarning($"<b>[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 파일 삭제: {file}</b> \n{content}");
            }
            if (deletedCount > 0)
                Debug.LogWarning($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 총 {deletedCount}개의 캐시 파일이 삭제되었습니다 (count {startCount})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 파일 삭제 중 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 분 단위 시간과 에이전트 타입을 기준으로 캐시 파일에 응답을 저장합니다.
    /// 문자열 포함 모든 타입을 저장합니다.
    /// </summary>
    protected void SaveCachedResponse<T>(T data)
    {
        if (data == null) return;

        try
        {
            var timeService = Services.Get<ITimeService>();
            if (timeService == null) return;

            var gt = timeService.CurrentTime; // 분 단위 키 구성
            var timeKey = $"{gt.year:D4}-{gt.month:D2}-{gt.day:D2}_{gt.hour:D2}-{gt.minute:D2}";
            var agentPart = string.IsNullOrEmpty(agentTypeOverride) ? "UNKNOWN" : agentTypeOverride;

            var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", "CachedLogs", actorName ?? "Unknown");
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            var lockObj = ActorCacheLocks.GetOrAdd(actorName ?? "Unknown", _ => new object());
            lock (lockObj)
            {
                // 현재 요청 컨텍스트 해시 재사용(없으면 안전하게 재계산)
                var msgHash = currentMsgHash ?? ComputeMessagesHash(GetHashKey());
                int countForThisSave = actor.CacheCount; // 저장 전 count 캡처
                var filePath = Path.Combine(baseDir, $"{countForThisSave}_{timeKey}_{agentPart}_{msgHash}.json");
                var json = JsonConvert.SerializeObject(data, Formatting.Indented, EnumAsStringJsonSettings);
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
                Debug.Log($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 저장: {filePath}");
                // 동일 count의 기존 파일 정리 (방금 저장한 파일은 제외)
                DeleteCacheFilesFromCountExcept(baseDir, countForThisSave, filePath);
                actor.CacheCount++; // 저장 후 증가
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지정된 count의 캐시 파일 중 keepFilePath를 제외하고 삭제합니다.
    /// </summary>
    private void DeleteCacheFilesFromCountExcept(string baseDir, int startCount, string keepFilePath)
    {
        try
        {
            int deletedCount = 0;
            var files = Directory.GetFiles(baseDir, $"{startCount}_*.json");
            foreach (var file in files)
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(keepFilePath), StringComparison.OrdinalIgnoreCase))
                    continue;
                var content = File.ReadAllText(file);
                File.Delete(file);
                deletedCount++;
                Debug.LogWarning($"<b>[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 파일 삭제: {file}</b> \n{content}");
            }
            if (deletedCount > 0)
                Debug.LogWarning($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 총 {deletedCount}개의 캐시 파일이 삭제되었습니다 (count {startCount})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{agentTypeOverride ?? "Unknown"}][{actorName}] 캐시 파일 삭제 중 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 입력 객체를 기반으로 고정 길이 해시(HEX)를 생성합니다.
    /// 캐시 파일명에 포함되어 동일 키일 때만 히트하도록 합니다.
    /// </summary>
    private string ComputeMessagesHash(object key)
    {
        try
        {
            var json = JsonConvert.SerializeObject(key ?? new { });
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
                var hash = sha.ComputeHash(bytes);
                var hex = BitConverter.ToString(hash).Replace("-", "");
                return hex.Substring(0, Math.Min(16, hex.Length)); // 16자 prefix 사용
            }
        }
        catch
        {
            return "NOHASH";
        }
    }
    protected abstract UniTask<T> Send<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null
    );

    #endregion

    #region 도구 사용 (공급자-중립 스키마)
    /// <summary>
    /// 공급자-중립 툴 스키마를 등록합니다. 구현체(GPT, Gemini 등)에서 각 공급자 형식으로 변환/저장합니다.
    /// </summary>
    public abstract void AddTools(params LLMToolSchema[] tools);
    public abstract void ClearTools();
    #endregion

    #region 설정
    /// <summary>
    /// 전역 직렬화 토글. true면 모든 LLMClient 요청을 순차적으로 처리합니다.
    /// </summary>
    public static void SetGlobalSerialization(bool enabled)
    {
        SerializeAllRequests = enabled;
        Debug.Log($"[LLMClient] Global serialization enabled = {SerializeAllRequests}");
    }
    /// <summary>
    /// 공급자-중립 포맷 스키마를 설정합니다. 구현체에서 각 공급자 형식의 ResponseFormat/generationConfig로 반영됩니다.
    /// </summary>
    public abstract void SetResponseFormat(LLMClientSchema schema);

    public abstract void SetTemperature(float temperature);
    #endregion

    #region 오류 방지 및 처리 헬퍼 메서드
    // Utility: sanitize JSON with trailing commas
    protected static string RemoveTrailingCommas(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        // Remove trailing commas before } or ]
        var pattern = @",\s*(\}|\])";
        return Regex.Replace(json, pattern, "$1");
    }

    // Utility: extract the outermost JSON object substring
    protected static string ExtractOutermostJsonObject(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        int firstBraceIndex = text.IndexOf('{');
        if (firstBraceIndex < 0) return text;
        int depth = 0;
        for (int i = firstBraceIndex; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text.Substring(firstBraceIndex, i - firstBraceIndex + 1);
                }
            }
        }
        // If braces are unbalanced, return from the first '{' to the end
        return text.Substring(firstBraceIndex);
    }

    protected void LogExceptionWithLocation(Exception ex, string context)
    {
        try
        {
            var st = new System.Diagnostics.StackTrace(ex, true);
            System.Diagnostics.StackFrame target = null;
            for (int i = 0; i < st.FrameCount; i++)
            {
                var f = st.GetFrame(i);
                var file = f.GetFileName();
                if (!string.IsNullOrEmpty(file)) { target = f; break; }
            }
            target ??= st.FrameCount > 0 ? st.GetFrame(0) : null;

            var method = target?.GetMethod();
            var fileName = target?.GetFileName() ?? "[unknown file]";
            var line = target?.GetFileLineNumber() ?? 0;
            var methodName = method != null ? $"{method.DeclaringType?.FullName}.{method.Name}" : "[unknown method]";

            Debug.LogError($"[GPT][{context}] {ex.GetType().Name}: {ex.Message}\n at {fileName}:{line}\n in {methodName}\nStackTrace:\n{ex}");
        }
        catch (Exception logEx)
        {
            Debug.LogError($"[GPT] Failed to log exception details: {logEx.Message}. Original error: {ex.Message}\nOriginal stack:\n{ex}");
        }
    }
    #endregion

}

public class Auth
{
    public string gpt_api_key;
    public string gemini_api_key;
    public string claude_api_key;
    public string organization;
}
public enum LLMClientProvider
{
    OpenAI,
    Anthropic,
    Gemini,
}

public class LLMClientProps
{
    //public string apiKey;
    public LLMClientProvider provider;
    public string model;
}

public class LLMClientSchema
{
    public string name = "";
    public string description = "";
    public JObject format;
}

/// <summary>
/// 공급자-중립 LLM 툴 스키마 (function/tool 선언용)
/// </summary>
public class LLMToolSchema
{
    public string name = "";
    public string description = "";
    public JObject format; // null 이면 파라미터 없음
}

public class LLMClientToolResponse<T>
{
    public string name = "";
    public T args;
}

public class ToolInvocationRecord
{
    public string name = "";
    public string argsJson = "{}";
}

public class LLMCacheEnvelope<T>
{
    public T payload;
    public List<ToolInvocationRecord> tools = new List<ToolInvocationRecord>();
}