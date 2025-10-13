using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ClothingDatabase", menuName = "Game/Clothing Database")]
public class ClothingDatabase : ScriptableObject
{
	[System.Serializable]
	public class Entry
	{
		public ActorId actorId;
		public ClothingType clothingType;
		public Gender gender;
		public GameObject fbxPrefab;
	}

	[SerializeField]
	private List<Entry> entries = new List<Entry>();

	private Dictionary<string, Entry> cache;

	private void OnEnable()
	{
		BuildCache();
	}

	private void OnValidate()
	{
		BuildCache();
		ValidateDuplicates();
	}

	private void BuildCache()
	{
		cache = new Dictionary<string, Entry>();
		if (entries == null) return;
		for (int i = 0; i < entries.Count; i++)
		{
			var e = entries[i];
			if (e == null) continue;
			var key = MakeKey(e.actorId, e.clothingType, e.gender);
			if (!cache.ContainsKey(key))
			{
				cache[key] = e;
			}
		}
	}

	private void ValidateDuplicates()
	{
		if (entries == null) return;
		var seen = new HashSet<string>();
		for (int i = 0; i < entries.Count; i++)
		{
			var e = entries[i];
			if (e == null) continue;
			var key = MakeKey(e.actorId, e.clothingType, e.gender);
			if (!seen.Add(key))
			{
				Debug.LogWarning($"[ClothingDatabase] Duplicate mapping for key={key}. Only the first entry will be used.", this);
			}
		}
	}

	private static string MakeKey(ActorId actorId, ClothingType type, Gender gender)
	{
		return $"{actorId}|{type}|{gender}";
	}

	public GameObject GetFbx(ActorId actorId, ClothingType type, Gender gender)
	{
		var key = MakeKey(actorId, type, gender);
		if (cache != null && cache.TryGetValue(key, out var entry) && entry != null)
		{
			return entry.fbxPrefab;
		}

		// Fallback: linear search (in case cache wasn't built in editor)
		if (entries != null)
		{
			for (int i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				if (e == null) continue;
				if (e.actorId == actorId && e.clothingType == type && e.gender == gender)
				{
					return e.fbxPrefab;
				}
			}
		}

		return null;
	}

	public GameObject GetFbxByEnglishName(string englishActorName, ClothingType type, Gender gender)
	{
		if (string.IsNullOrWhiteSpace(englishActorName)) return null;
		if (System.Enum.TryParse<ActorId>(englishActorName, out var actorId))
		{
			return GetFbx(actorId, type, gender);
		}
		// If not parsable, no mapping
		return null;
	}

	public static ClothingDatabase Load()
	{
		// Requires an asset placed at Assets/Resources/ClothingDatabase.asset
		return Resources.Load<ClothingDatabase>("ClothingDatabase");
	}
}


