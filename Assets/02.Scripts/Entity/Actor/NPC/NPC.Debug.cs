using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

public abstract partial class NPC
{
	[Button("Log Current Situation")]
	private void LogCurrentSituation()
	{
		if (actionAgent == null)
		{
			Debug.LogWarning($"[{Name}] AI Agent가 초기화되지 않았습니다.");
			return;
		}

		try
		{
			Debug.Log($"[{Name}] 현재 상황 정보:");

			var timeService = Services.Get<ITimeService>();
			if (timeService != null)
			{
				var currentTime = timeService.CurrentTime;
				var dayOfWeek = GameTime.GetDayOfWeekString(currentTime.GetDayOfWeek());
				Debug.Log($"  시간: {currentTime.year}년 {currentTime.month}월 {currentTime.day}일 {dayOfWeek} {currentTime.hour:00}:{currentTime.minute:00}");
			}

			Debug.Log($"  상태: 배고픔({Hunger}/100), 갈증({Thirst}/100), 체력({Stamina}/100), 졸림({Sleepiness}/100), 스트레스({Stress}/100), 만족감({MentalPleasure})");

			var locationService = Services.Get<ILocationService>();
			if (locationService != null)
			{
				var currentArea = locationService.GetArea(curLocation);
				if (currentArea != null)
				{
					var actors = locationService.GetActor(currentArea, this);
					var nearbyActors = actors.Where(a => a != this).ToList();

					if (nearbyActors.Count > 0)
					{
						Debug.Log($"  주변 인물: {string.Join(", ", nearbyActors.Select(a => $"{a.Name}({GetActorBriefStatus(a)})"))}");
					}
					else
					{
						Debug.Log("  주변 인물: 없음");
					}
				}
			}

			Debug.Log($"  위치: {curLocation?.locationName ?? "알 수 없음"}");

		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] 상황 정보 로그 출력 실패: {ex.Message}");
		}
	}

	private string GetActorBriefStatus(Actor actor)
	{
		if (actor == null) return "알 수 없음";

		var statusList = new List<string>();

		if (actor is MainActor mainActor)
		{
			if (mainActor.IsSleeping) statusList.Add("수면");
			if (mainActor.IsPerformingActivity) statusList.Add("활동중");
		}

		if (actor.Sleepiness > 80) statusList.Add("매우졸림");
		else if (actor.Sleepiness > 60) statusList.Add("졸림");

		if (actor.Hunger > 80) statusList.Add("매우배고픔");
		else if (actor.Hunger > 60) statusList.Add("배고픔");

		if (actor.Stress > 80) statusList.Add("매우스트레스");
		else if (actor.Stress > 60) statusList.Add("스트레스");

		return statusList.Count > 0 ? string.Join(",", statusList) : "정상";
	}
}
