#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FindInterfaceImplementers
{
	private static void SelectByInterface<T>(string interfaceLabel) where T : class
	{
		var components = Resources
			.FindObjectsOfTypeAll<MonoBehaviour>()
			.Where(c => c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid())
			.OfType<T>()
			.Select(c => (c as MonoBehaviour)?.gameObject)
			.Where(go => go != null)
			.Distinct()
			.ToArray();

		Selection.objects = components;
		if (components.Length > 0)
		{
			Debug.Log($"[Editor Tool] 씬에서 {components.Length}개의 {interfaceLabel} 오브젝트를 찾았습니다.");
		}
		else
		{
			Debug.LogWarning($"[Editor Tool] 씬에서 {interfaceLabel} 인터페이스를 구현한 오브젝트를 찾지 못했습니다.");
		}
	}

	[MenuItem("Tools/Find Objects With Interface/Find ILocation")]
	private static void FindILocation()
	{
		SelectByInterface<ILocation>("ILocation");
	}

	[MenuItem("Tools/Find Objects With Interface/Find ICollectible")]
	private static void FindICollectible()
	{
		SelectByInterface<ICollectible>("ICollectible");
	}

	[MenuItem("Tools/Find Objects With Interface/Find IInteractable")]
	private static void FindIInteractable()
	{
		SelectByInterface<IInteractable>("IInteractable");
	}

	[MenuItem("Tools/Find Objects With Interface/Find IUsable")]
	private static void FindIUsable()
	{
		SelectByInterface<IUsable>("IUsable");
	}

	// Alias menu for user's convenience ("iUsable")
	[MenuItem("Tools/Find Objects With Interface/Find iUsable (alias)")]
	private static void FindiUsableAlias()
	{
		SelectByInterface<IUsable>("IUsable");
	}
}
#endif


