#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LocalizationDiagnosticsTools
{
	[MenuItem("Tools/Localization/Select ILocation with empty KR name")]
	private static void SelectILocationWithEmptyKrName()
	{
		var sceneComponents = Resources
			.FindObjectsOfTypeAll<MonoBehaviour>()
			.Where(c => c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid());

		var targets = new List<GameObject>();
		foreach (var comp in sceneComponents)
		{
			if (comp is not ILocation)
				continue;

			// Prefer Entity's _nameKr; fallback to Area's _locationNameKr
			var so = new SerializedObject(comp);
			var nameKr = so.FindProperty("_nameKr");
			string kr = nameKr != null ? nameKr.stringValue : null;
			if (nameKr == null)
			{
				var areaNameKr = so.FindProperty("_locationNameKr");
				kr = areaNameKr != null ? areaNameKr.stringValue : kr;
			}

			if (string.IsNullOrEmpty(kr))
			{
				targets.Add(comp.gameObject);
			}
		}

		if (targets.Count > 0)
		{
			Selection.objects = targets.Distinct().ToArray();
			foreach (var go in targets)
			{
				Debug.Log($"[Localization] Empty KR name on: {go.name} (Path: {GetHierarchyPath(go)})", go);
			}
			Debug.Log($"[Localization] Found {targets.Count} object(s) implementing ILocation with empty KR name. They have been selected.");
		}
		else
		{
			Selection.objects = System.Array.Empty<Object>();
			Debug.Log("[Localization] No ILocation objects with empty KR name found in the scene.");
		}
	}

	private static string GetHierarchyPath(GameObject obj)
	{
		if (obj == null) return string.Empty;
		var stack = new Stack<string>();
		var t = obj.transform;
		while (t != null)
		{
			stack.Push(t.name);
			t = t.parent;
		}
		return string.Join("/", stack);
	}
}
#endif
