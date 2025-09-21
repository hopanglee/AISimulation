using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;

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

    protected override void Awake()
	{
		base.Awake();
		brain = new(this);
		brain.memoryManager.ClearShortTermMemory();
		brain?.memoryManager?.AddShortTermMemory(yesterdaySleepTime, "event_occurred", "교통 사고", $"{yesterdaySleepLocation}에서 교통사고가 발생", null);
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

        // 잠들 때 메모리 복원 (그날의 초기 상태로 되돌림)
        await RestoreAllMemoryFiles();
    }

    public override async UniTask WakeUp()
    {
        if (!useMemoryResetSystem)
        {
            await base.WakeUp();
            return;
        }

        if (!isSleeping)
        {
            Debug.LogWarning($"[{Name}] Not sleeping!");
            return;
        }


        // 기상 시 메모리 백업 (그날의 초기 상태 저장)
        await BackupAllMemoryFiles();

        // 백업 완료 후 평범한 base.WakeUp() 실행
        await base.WakeUp();
    }
}
