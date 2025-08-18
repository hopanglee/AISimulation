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

		var locationService = Services.Get<ILocationService>();
		var currentArea = locationService.GetArea(curLocation);
		if (currentArea != null)
		{
			var actors = locationService.GetActor(currentArea, this);
			foreach (var foundActor in actors)
			{
				if (foundActor.Name == actorName || foundActor.name == actorName)
				{
					return foundActor;
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
