using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class KonbiniPrefabCleaner
{
#if UNITY_EDITOR
    private const string TargetFolder = "Assets/03.Prefabs/Konbini";

    [MenuItem("Custom/Konbini/Replace Missing Scripts With BasicFoodItem")]
    public static void ReplaceMissingScriptsInKonbiniPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetFolder });
        int totalPrefabs = 0;
        int totalReplaced = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                continue;

            try
            {
                int replacedInThis = ReplaceMissingScriptsAndAddBasicFoodItem(root);
                if (replacedInThis > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    totalReplaced += replacedInThis;
                    totalPrefabs++;
                    Debug.Log($"[KonbiniPrefabCleaner] {path}: Removed {replacedInThis} missing script component(s) and added BasicFoodItem.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (totalReplaced > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.LogWarning($"[KonbiniPrefabCleaner] Processed {guids.Length} prefabs under {TargetFolder}. Updated {totalPrefabs} prefab(s), removed {totalReplaced} missing script component(s) in total.");
        }
        else
        {
            Debug.Log($"[KonbiniPrefabCleaner] No missing scripts found under {TargetFolder}.");
        }
    }

    [MenuItem("Custom/Konbini/Force HideChild For Food/Drink/Alcohol (Rules Only)")]
    public static void ForceHideChildForConsumables()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetFolder });
        int totalPrefabs = 0;
        int totalChanged = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                continue;

            try
            {
                int changed = 0;
                var entities = root.GetComponentsInChildren<Entity>(true);
                foreach (var ent in entities)
                {
                    if (ent == null) continue;
                    var typeName = ent.GetType().Name;
                    if (typeName == "BasicFoodItem" || typeName == "BasicDrink" || typeName == "Alcohol")
                    {
                        if (!ent.IsHideChild)
                        {
                            ent.IsHideChild = true;
                            changed++;
                        }
                    }
                }

                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    totalPrefabs++;
                    totalChanged += changed;
                    Debug.Log($"[KonbiniPrefabCleaner] {path}: Force set IsHideChild=true on {changed} component(s).");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (totalChanged > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.LogWarning($"[KonbiniPrefabCleaner] Force-hid {totalChanged} component(s) across {totalPrefabs} prefab(s) under {TargetFolder}.");
        }
        else
        {
            Debug.Log($"[KonbiniPrefabCleaner] No BasicFoodItem/BasicDrink/Alcohol found that required changes under {TargetFolder}.");
        }
    }

    [MenuItem("Custom/Konbini/Apply BasicFoodItem Rules Only")]
    public static void ApplyBasicFoodItemRulesOnly()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetFolder });
        int totalPrefabs = 0;
        int totalNormalized = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                continue;

            try
            {
                int normalized = NormalizeBasicFoodItemFlags(root);
                if (normalized > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    totalPrefabs++;
                    totalNormalized += normalized;
                    Debug.Log($"[KonbiniPrefabCleaner] {path}: normalized {normalized} BasicFoodItem(s).");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (totalNormalized > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.LogWarning($"[KonbiniPrefabCleaner] Normalized {totalNormalized} BasicFoodItem(s) across {totalPrefabs} prefab(s) under {TargetFolder}.");
        }
        else
        {
            Debug.Log($"[KonbiniPrefabCleaner] No changes were necessary under {TargetFolder}.");
        }
    }

    private static int ReplaceMissingScriptsAndAddBasicFoodItem(GameObject root)
    {
        int deleteCount = 0;

        // Include root and all children (inactive included)
        var queue = new List<GameObject> { root };
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null)
            {
                queue.Add(t.gameObject);
            }
        }

        foreach (var go in queue)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                deleteCount += missing;

                // Add BasicFoodItem component if not already present
                if (go.GetComponent<BasicFoodItem>() == null)
                {
                    go.AddComponent<BasicFoodItem>();
                }
            }
        }

        return deleteCount;
    }

    private static int NormalizeBasicFoodItemFlags(GameObject root)
    {
        int changed = 0;
        var comps = root.GetComponentsInChildren<BasicFoodItem>(true);
        foreach (var comp in comps)
        {
            var go = comp.gameObject;
            if (!go.name.StartsWith("SM"))
            {
                bool modified = false;
                if (!comp.IsHideChild)
                {
                    comp.IsHideChild = true;
                    modified = true;
                }
                if (comp.Name != go.name)
                {
                    comp.Name = go.name;
                    modified = true;
                }
                if (modified) changed++;
            }
        }
        return changed;
    }
#endif
}


