#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AreaLocalizationTools
{
	[MenuItem("Tools/Area/Sync KR Names To English")] 
	private static void SyncLocationNameKrToLocationName()
	{
		var areas = Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
		int updated = 0;
		foreach (var area in areas)
		{
			if (area == null) continue;
			var so = new SerializedObject(area);
			var krProp = so.FindProperty("_locationNameKr");
			var nameProp = so.FindProperty("_locationName");
			if (krProp == null || nameProp == null) continue;
			string current = krProp.stringValue;
			string target = nameProp.stringValue;
			if (current != target)
			{
				Undo.RecordObject(area, "Sync Area locationNameKr");
				krProp.stringValue = target;
				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(area);
				updated++;
			}
		}
		if (updated > 0)
		{
			EditorSceneManager.MarkAllScenesDirty();
			Debug.Log($"[AreaLocalizationTools] Updated locationNameKr on {updated} Area component(s).");
		}
		else
		{
			Debug.Log("[AreaLocalizationTools] No changes were necessary.");
		}
	}
}
#endif


