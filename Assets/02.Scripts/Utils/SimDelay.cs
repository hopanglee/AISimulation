using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
		var timeService = Services.Get<ITimeService>();
		if (timeService == null || simMinutes <= 0)
		{
			await UniTask.Yield();
			return;
		}
		long target = timeService.CurrentTime.ToMinutes() + simMinutes;
		while (timeService.CurrentTime.ToMinutes() < target)
		{
			if (token.IsCancellationRequested) return;
			await UniTask.Yield();
		}
	}

	public static UniTask DelaySimHours(int simHours, CancellationToken token = default)
	{
		int minutes = Mathf.Max(0, simHours) * 60;
		return DelaySimMinutes(minutes, token);
	}
}
