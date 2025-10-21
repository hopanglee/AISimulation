using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;
using Memory;

public class HinoMaori : MainActor
{
    [Header("Memory Reset System")]
    [SerializeField]
    private bool useMemoryResetSystem = true;
    private async UniTask BackupAllMemoryFiles()
    {
        if (!useMemoryResetSystem) return;

        try
        {
            // CharacterInfo 백업
            var characterMemoryManager = new CharacterMemoryManager(this);
            await characterMemoryManager.BackupCharacterInfoAsync();

            // Short-term, Long-term Memory 백업 (MemoryManager를 통해)
            if (brain?.memoryManager != null)
            {
                await brain.memoryManager.BackupAllMemoriesAsync();
            }

            // Relationship Memory 백업
            var relationshipMemoryManager = new RelationshipMemoryManager(this);
            await relationshipMemoryManager.BackupMemoryFilesAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{Name}] Failed to backup memory files: {e.Message}");
        }
    }
    private async UniTask RestoreAllMemoryFiles()
    {
        if (!useMemoryResetSystem) return;

        try
        {
            // CharacterInfo 복원
            var characterMemoryManager = new CharacterMemoryManager(this);
            await characterMemoryManager.RestoreCharacterInfoAsync();

            // Short-term, Long-term Memory 복원 (MemoryManager를 통해)
            if (brain?.memoryManager != null)
            {
                await brain.memoryManager.RestoreAllMemoriesAsync();
            }

            // Relationship Memory 복원
            var relationshipMemoryManager = new RelationshipMemoryManager(this);
            await relationshipMemoryManager.RestoreMemoryFilesAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{Name}] Failed to restore memory files: {e.Message}");
        }
    }

    private LongTermMemory CloneWithMaskedContent(LongTermMemory src, float maskRatio)
    {
        if (src == null) return null;
        var clone = new LongTermMemory
        {
            timestamp = src.timestamp,
            type = src.type,
            category = src.category,
            content = MaskContent(src.content, maskRatio),
            emotions = src.emotions != null ? new List<Emotions>(src.emotions) : new List<Emotions>(),
            relatedActors = src.relatedActors != null ? new List<string>(src.relatedActors) : new List<string>(),
            location = src.location
        };
        return clone;
    }

    private string MaskContent(string content, float ratio)
    {
        if (string.IsNullOrEmpty(content) || ratio <= 0f) return content;
        var chars = content.ToCharArray();
        int maskCount = Mathf.Clamp(Mathf.CeilToInt(chars.Count(c => !char.IsWhiteSpace(c)) * ratio), 1, chars.Length);

        // 인덱스 목록(공백 제외) 만들기
        var candidateIdx = new List<int>();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsWhiteSpace(chars[i])) candidateIdx.Add(i);
        }
        if (candidateIdx.Count == 0) return content;

        // 셔플 후 앞에서 maskCount개 선택
        for (int i = 0; i < candidateIdx.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, candidateIdx.Count);
            (candidateIdx[i], candidateIdx[j]) = (candidateIdx[j], candidateIdx[i]);
        }
        int take = Mathf.Min(maskCount, candidateIdx.Count);
        for (int k = 0; k < take; k++)
        {
            chars[candidateIdx[k]] = '?';
        }
        return new string(chars);
    }
    protected override void Awake()
	{
		base.Awake();
		//brain = new(this);
		brain.memoryManager.ClearShortTermMemory();
		brain.memoryManager.AddShortTermMemory(yesterdaySleepTime, $"차가 매우 빠르게 다가와 부딪침", "쾅!", yesterdaySleepLocation, null);
	}
    public override async UniTask Sleep(int? minutes = null)
    {
        if (!useMemoryResetSystem)
        {
            await base.Sleep(minutes);
            return;
        }

        // 메모리 초기화 시스템이 활성화된 경우
        if (isSleeping)
        {
            Debug.LogWarning($"[{Name}] Already sleeping!");
            return;
        }

        var timeService = Services.Get<ITimeService>();
        sleepStartTime = timeService.CurrentTime;

        // 기상 시간 계산
        var currentTime = timeService.CurrentTime;

        if (minutes.HasValue)
        {
            // 지정된 시간(분) 후에 일어나도록 설정
            long totalMinutes = currentTime.ToMinutes() + minutes.Value;
            wakeUpTime = GameTime.FromMinutes(totalMinutes);
        }
        else
        {
            // 기본 기상 시간으로 설정 (다음 날 7시)
            wakeUpTime = new GameTime(
                currentTime.year,
                currentTime.month,
                currentTime.day + 1,
                7, // 기본 기상 시간 7시
                0
            );

            // 월/연도 조정
            int daysInMonth = GameTime.GetDaysInMonth(wakeUpTime.year, wakeUpTime.month);
            if (wakeUpTime.day > daysInMonth)
            {
                wakeUpTime.day = 1;
                wakeUpTime.month++;
                if (wakeUpTime.month > 12)
                {
                    wakeUpTime.month = 1;
                    wakeUpTime.year++;
                }
            }
        }

        isSleeping = true;
        Debug.Log($"[{Name}] Started sleeping at {sleepStartTime}. Will wake up at {wakeUpTime}");

        // 현재 LTM 스냅샷(복원 전)
        List<LongTermMemory> preRestoreLtm = null;
        try
        {
            preRestoreLtm = brain?.memoryManager?.GetLongTermMemories()?.ToList();
        }
        catch { }

        // 잠들 때 메모리 복원 (그날의 초기 상태로 되돌림)
        await RestoreAllMemoryFiles();

        // 복원 후: 이전과 비교해 새로 생긴 LTM 중 가장 최근 1개만 보존하여 누적
        try
        {
            var postRestoreLtm = brain?.memoryManager?.GetLongTermMemories() ?? new List<LongTermMemory>();
            if (preRestoreLtm != null && preRestoreLtm.Count > 0)
            {
                string Sig(LongTermMemory m)
                {
                    var ts = m?.timestamp != null ? m.timestamp.ToMinutes().ToString() : "0";
                    var who = m?.relatedActors != null ? string.Join(",", m.relatedActors) : string.Empty;
                    return string.Join("|", ts, m?.type, m?.category, m?.content, m?.location, who);
                }

                var postSet = new HashSet<string>(postRestoreLtm.Select(Sig));
                var newOnes = preRestoreLtm
                    .Where(m => !postSet.Contains(Sig(m)))
                    .OrderByDescending(m => m?.timestamp != null ? m.timestamp.ToMinutes() : 0)
                    .ToList();

                var ts2 = Services.Get<ITimeService>();
                if (ts2 != null)
                {
                    var today = ts2.CurrentTime;
                    var todayNewOnes = newOnes
                        .Where(m => m?.timestamp != null
                            && m.timestamp.year == today.year
                            && m.timestamp.month == today.month
                            && m.timestamp.day == today.day)
                        .ToList();

                    if (todayNewOnes.Count > 0)
                    {
                        // 무작위 2개까지 선택 (중복 없이)
                        var indices = Enumerable.Range(0, todayNewOnes.Count).ToList();
                        // 간단 셔플
                        for (int i = 0; i < indices.Count; i++)
                        {
                            int j = UnityEngine.Random.Range(i, indices.Count);
                            (indices[i], indices[j]) = (indices[j], indices[i]);
                        }

                        int take = Mathf.Min(2, indices.Count);
                        var toPreserve = new List<LongTermMemory>();
                        for (int k = 0; k < take; k++)
                        {
                            var src = todayNewOnes[indices[k]];
                            var masked = CloneWithMaskedContent(src, 0.10f); // 10% 마스킹
                            toPreserve.Add(masked);
                        }
                        if (toPreserve.Count > 0)
                        {
                            brain.memoryManager.AddLongTermMemories(toPreserve);
                            Debug.Log($"[{Name}] Preserved {toPreserve.Count} TODAY LTM across restore (masked 10%).");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[{Name}] Failed to preserve LTM across restore: {ex.Message}");
        }
    }

    public override void WakeUp()
    {
        if (!useMemoryResetSystem)
        {
            base.WakeUp();
            return;
        }

        if (!isSleeping)
        {
            Debug.LogWarning($"[{Name}] Not sleeping!");
            return;
        }

        RunWakeUpAsync().Forget();
    }

    private async UniTask RunWakeUpAsync()
    {
        try
        {
            TimeManager.StartTimeStop();
            // 기상 시 메모리 백업 (그날의 초기 상태 저장)
            await BackupAllMemoryFiles();
        }
        finally
        {
            TimeManager.EndTimeStop();
        }

        // 백업 완료 후 평범한 base.WakeUp() 실행
        base.WakeUp();
    }
}
