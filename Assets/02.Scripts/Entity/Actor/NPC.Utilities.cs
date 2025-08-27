using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract partial class NPC
{
	protected Actor FindActorByName(string actorName)
	{
		if (string.IsNullOrEmpty(actorName))
			return null;

		// Sensor의 lookable에서 검색 (추가 감지 영역 포함)
		if (sensor == null)
		{
			// 폴백: 현재 Area에서 LocationService로 검색
			var locationService = Services.Get<ILocationService>();
			var currentArea = locationService.GetArea(curLocation);
			if (currentArea != null)
			{
				var actors = locationService.GetActor(currentArea, this);
				foreach (var foundActor in actors)
				{
					if (string.Equals(foundActor.Name, actorName, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(foundActor.name, actorName, StringComparison.OrdinalIgnoreCase))
					{
						return foundActor;
					}
				}
			}
			Debug.LogWarning($"[{Name}] Sensor가 없어 Area 기반으로 검색했으나 Actor를 찾지 못했습니다: {actorName}");
			return null;
		}

		var lookable = sensor.GetLookableEntities();
		if (lookable == null || lookable.Count == 0)
		{
			sensor.UpdateAllSensors();
			lookable = sensor.GetLookableEntities();
		}

		if (lookable != null)
		{
			// 1) full key로 먼저 매칭 시도 (예: "Hino Maori in Seating Area")
			foreach (var kv in lookable)
			{
				if (string.Equals(kv.Key, actorName, StringComparison.OrdinalIgnoreCase) && kv.Value is Actor ak)
				{
					return ak;
				}
			}

			// 2) 값의 표시 이름으로 매칭 (예: "Hino Maori")
			foreach (var kv in lookable)
			{
				if (kv.Value is Actor a)
				{
					// 이름 또는 GameObject 이름으로 매칭 (대소문자 무시)
					if (string.Equals(a.Name, actorName, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(a.name, actorName, StringComparison.OrdinalIgnoreCase))
					{
						return a;
					}
				}
			}
		}

		Debug.LogWarning($"[{Name}] Actor를 찾을 수 없음: {actorName}");
		return null;
	}

	protected string GetFormattedCurrentTime()
	{
		if (Services.Get<ITimeService>() == null)
			return "[시간불명]";
		var currentTime = Services.Get<ITimeService>().CurrentTime;
		return $"[{currentTime.hour:00}:{currentTime.minute:00}]";
	}

	public override string GetStatusDescription()
	{
		string baseStatus = base.GetStatusDescription();
		string npcStatus = $"역할: {npcRole}";
		if (!string.IsNullOrEmpty(baseStatus))
		{
			return $"{baseStatus} | {npcStatus}";
		}
		return npcStatus;
	}

}
