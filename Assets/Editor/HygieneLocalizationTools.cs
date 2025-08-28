#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HygieneLocalizationTools
{
	[MenuItem("Tools/Hygiene/Sync NameKr for BodyWash (바디워시)")]
	private static void SyncNameKrForBodyWash()
	{
		int updated = SyncNameKrByType<BodyWash>("Bodywash", "바디워시");
		Report("BodyWash", updated);
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for Shampoo (샴푸)")]
	private static void SyncNameKrForShampoo()
	{
		int updated = SyncNameKrByType<Shampoo>("Shampoo", "샴푸");
		Report("Shampoo", updated);
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for BodyWash & Shampoo")] 
	private static void SyncNameKrForBoth()
	{
		int total = 0;
		total += SyncNameKrByType<BodyWash>("Bodywash", "바디워시");
		total += SyncNameKrByType<Shampoo>("Shampoo", "샴푸");
		if (total > 0) EditorSceneManager.MarkAllScenesDirty();
		Debug.Log($"[HygieneLocalizationTools] Updated NameKr/IsHideChild on {total} object(s) in total.");
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for ShowerHead (샤워기) & HideChild=true")] 
	private static void SyncNameKrForShowerHead()
	{
		int updated = SyncNameKrByType<ShowerHead>("Shower Head", "샤워기", true, null);
		Report("ShowerHead", updated);
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for Chair (의자), set PrepositionKr and HideChild")]
	private static void SyncNameKrForChair()
	{
		int updated = SyncNameKrByType<Chair>("Chair", "의자", false, " 위의 ");
		Report("Chair", updated);
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for Locker (락커), set PrepositionKr and HideChild")]
	private static void SyncNameKrForLocker()
	{
		int updated = SyncNameKrByType<Locker>("Locker", "락커", true, " 안의 ");
		Report("Locker", updated);
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for Mirror (거울) & HideChild=true")]
	private static void SyncNameKrForMirror()
	{
		int updated = SyncNameKrByType<Mirror>("Mirror", "거울", true, null);
		Report("Mirror", updated);
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for Bench (소파), set PrepositionKr and HideChild")]
	private static void SyncNameKrForBench()
	{
		int updated = SyncNameKrByType<Bench>("Sofa", "소파", false, " 위의 ");
		Report("Bench", updated);
	}

	[MenuItem("Tools/Hygiene/Sync NameKr for DiningTable (탁자), set PrepositionKr and HideChild")]
	private static void SyncNameKrForDiningTable()
	{
		int updated = SyncNameKrByType<DiningTable>("Table", "탁자", false, " 위의 ");
		Report("DiningTable", updated);
	}

	[MenuItem("Tools/Hygiene/Rename DiningTable NameKr 탁자→식탁 (KR only)")]
	private static void RenameDiningTableKrOnly()
	{
		int updated = ReplaceNameKrTokenByType<DiningTable>("탁자", "식탁");
		Report("DiningTable NameKr Rename", updated);
	}

	private static int SyncNameKrByType<T>(string englishToken, string koreanToken) where T : Component
	{
		return SyncNameKrByType<T>(englishToken, koreanToken, true, null);
	}

	private static int SyncNameKrByType<T>(string englishToken, string koreanToken, bool? targetHideChild, string prepositionKr) where T : Component
	{
		var items = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
		int updated = 0;
		foreach (var comp in items)
		{
			if (comp == null) continue;
			// Access base Entity's serialized fields: _nameKr, _name, _isHideChild
			var so = new SerializedObject(comp);
			var nameKrProp = so.FindProperty("_nameKr");
			var nameProp = so.FindProperty("_name");
			var hideChildProp = so.FindProperty("_isHideChild");
			var prepositionKrProp = so.FindProperty("_prepositionKr");

			// If not found on this component, try to find on Entity base via GetComponent<Entity>()
			if (nameKrProp == null || hideChildProp == null || nameProp == null)
			{
				var entity = comp.GetComponent<Entity>();
				if (entity == null) continue;
				so = new SerializedObject(entity);
				nameKrProp = so.FindProperty("_nameKr");
				nameProp = so.FindProperty("_name");
				hideChildProp = so.FindProperty("_isHideChild");
				prepositionKrProp = so.FindProperty("_prepositionKr");
				if (nameKrProp == null || hideChildProp == null)
					continue;
			}

			string sourceName = nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)
				? nameProp.stringValue
				: comp.gameObject.name;

			string replaced = sourceName.Replace(englishToken, koreanToken);
			bool changed = false;
			if (nameKrProp.stringValue != replaced)
			{
				Undo.RecordObject(so.targetObject, "Sync NameKr");
				nameKrProp.stringValue = replaced;
				changed = true;
			}
			if (hideChildProp != null && targetHideChild.HasValue && hideChildProp.boolValue != targetHideChild.Value)
			{
				if (!changed) Undo.RecordObject(so.targetObject, "Set IsHideChild");
				hideChildProp.boolValue = targetHideChild.Value;
				changed = true;
			}

			if (prepositionKrProp != null && prepositionKr != null && prepositionKrProp.stringValue != prepositionKr)
			{
				if (!changed) Undo.RecordObject(so.targetObject, "Set PrepositionKr");
				prepositionKrProp.stringValue = prepositionKr;
				changed = true;
			}

			if (changed)
			{
				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(so.targetObject);
				updated++;
			}
		}

		if (updated > 0)
		{
			EditorSceneManager.MarkAllScenesDirty();
		}
		return updated;
	}

	private static int ReplaceNameKrTokenByType<T>(string fromKr, string toKr) where T : Component
	{
		var items = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
		int updated = 0;
		foreach (var comp in items)
		{
			if (comp == null) continue;
			var so = new SerializedObject(comp);
			var nameKrProp = so.FindProperty("_nameKr");
			if (nameKrProp == null)
			{
				var entity = comp.GetComponent<Entity>();
				if (entity == null) continue;
				so = new SerializedObject(entity);
				nameKrProp = so.FindProperty("_nameKr");
				if (nameKrProp == null) continue;
			}
			string cur = nameKrProp.stringValue ?? string.Empty;
			string replaced = cur.Replace(fromKr, toKr);
			if (replaced != cur)
			{
				Undo.RecordObject(so.targetObject, "Rename NameKr Token");
				nameKrProp.stringValue = replaced;
				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(so.targetObject);
				updated++;
			}
		}
		if (updated > 0) EditorSceneManager.MarkAllScenesDirty();
		return updated;
	}

	private static void Report(string label, int count)
	{
		if (count > 0)
		{
			Debug.Log($"[HygieneLocalizationTools] Updated {label}: {count} object(s).");
		}
		else
		{
			Debug.Log($"[HygieneLocalizationTools] No {label} objects required changes.");
		}
	}
}
#endif
