using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading.Tasks;

public static class SimDelay
{
	// Minute-level simulation; seconds are rounded up to minutes
	public static async UniTask DelaySimSeconds(int simSeconds, CancellationToken token = default)
	{
		int minutes = Mathf.CeilToInt(simSeconds / 60f);
		if (minutes <= 0) minutes = 1; // minute-level resolution
		await DelaySimMinutes(minutes, token);
	}

	public static async UniTask DelaySimMinutes(int simMinutes, CancellationToken token = default)
	{
		// UseGPT가 false인 경우 Task.Delay 사용 (Debug 모드)
		if (IsDebugMode())
		{
			Debug.Log($"[Debug Mode] {simMinutes}분 지연 시작");
			// 시뮬레이션 시간 1분당 실제 시간 1초
			
			float delaySeconds = simMinutes * 1f;
			await Task.Delay((int)(delaySeconds * 1000), token);
			return;
		}
		else
		{
			Debug.Log($"[Simulation Mode] {simMinutes}분 지연 시작");
		}
		
		// 개선된 로직: 시뮬레이션 시간(틱 단위) 기반 정밀 지연
		var timeService = Services.Get<ITimeService>();
		if (timeService == null || simMinutes <= 0)
		{
			await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
			return;
		}
		// 현재 누적 틱에 정확히 simMinutes*60초를 더한 목표 틱을 설정
		double startTicks = timeService.GetTotalTicks();
		double targetTicks = startTicks + (double)simMinutes * 60.0;
		while (timeService.GetTotalTicks() < targetTicks)
		{
			if (token.IsCancellationRequested) return;
			await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
		}
	}

	public static UniTask DelaySimHours(int simHours, CancellationToken token = default)
	{
		int minutes = Mathf.Max(0, simHours) * 60;
		return DelaySimMinutes(minutes, token);
	}

	/// <summary>
	/// Debug 모드인지 확인 (UseGPT가 false인 경우)
	/// </summary>
	private static bool IsDebugMode()
	{
		// 씬에서 첫 번째 Actor를 찾아서 UseGPT 상태 확인
		var actors = Object.FindObjectsByType<Actor>(FindObjectsSortMode.None);
		if (actors.Length > 0)
		{
			// 첫 번째 Actor의 UseGPT 상태를 기준으로 판단
			return !actors[0].UseGPT;
		}
		
		// Actor가 없으면 기본적으로 Debug 모드가 아님
		return false;
	}
}
