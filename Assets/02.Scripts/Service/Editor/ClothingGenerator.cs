using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClothingGenerator
{
    private const string ClothesRoot = "Assets/03.Prefabs/Clothes";
    private const string FemaleClothesFolder = ClothesRoot + "/Female";
    private const string MaleClothesFolder = ClothesRoot + "/Male";

    private const string FemaleFbxFolder = "Assets/03.Prefabs/Actors/MainCharacter/Aoi/Models Mecanim";
    private const string MaleFbxFolder = "Assets/03.Prefabs/Actors/MainCharacter/Taichi/Models Mecanim";

    private static readonly Dictionary<ClothingType, string[]> ClothingTypeToFbxTokensFemale = new()
    {
        { ClothingType.Casualwear, new[]{ "casualwear" } },
        { ClothingType.Schoolwear, new[]{ "schoolwear" } },
        { ClothingType.Swimwear,   new[]{ "swimwear" } },
        { ClothingType.Blazer,     new[]{ "blazer" } },
        { ClothingType.Apron,      new[]{ "apron" } },
        { ClothingType.Bathtowel,  new[]{ "bathtowel" } },
        { ClothingType.Pajamas,    new[]{ "pajamas" } },
        { ClothingType.Uniform,    new[]{ "uniform" } },
        { ClothingType.Gymclothes, new[]{ "gymclothes" } },
        { ClothingType.Leotard,    new[]{ "leotard" } },
        { ClothingType.Naked,      new[]{ "naked" } },
    };

    private static readonly Dictionary<ClothingType, string[]> ClothingTypeToFbxTokensMale = new()
    {
        { ClothingType.Casualwear, new[]{ "casualwear" } },
        { ClothingType.Schoolwear, new[]{ "schoolwear" } },
        { ClothingType.Swimwear,   new[]{ "swimwear" } },
        { ClothingType.Blazer,     new[]{ "blazer" } },
        { ClothingType.Apron,      new[]{ "apron" } },
        { ClothingType.Bathtowel,  new[]{ "bathtowel" } },
        { ClothingType.Pajamas,    new[]{ "pajamas" } },
        { ClothingType.Uniform,    new[]{ "uniform" } },
        { ClothingType.Jersey,     new[]{ "jersey" } },
        { ClothingType.Naked,      new[]{ "naked" } },
    };

    [MenuItem("Tools/Generate Clothing Prefabs (Female)")]
    public static void GenerateFemale()
    {
        GenerateForGender(Gender.Female, FemaleClothesFolder, FemaleFbxFolder, ClothingTypeToFbxTokensFemale);
    }

    [MenuItem("Tools/Generate Clothing Prefabs (Male)")]
    public static void GenerateMale()
    {
        GenerateForGender(Gender.Male, MaleClothesFolder, MaleFbxFolder, ClothingTypeToFbxTokensMale);
    }

    private static void GenerateForGender(
        Gender gender,
        string targetFolder,
        string fbxFolder,
        Dictionary<ClothingType, string[]> map)
    {
        if (!AssetDatabase.IsValidFolder(targetFolder))
        {
            Debug.LogError($"Target folder not found: {targetFolder}");
            return;
        }

        // Pick a template Clothing prefab from the target folder (first found)
        string templatePath = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(templatePath))
        {
            Debug.LogError($"No template prefab found in {targetFolder}. Create one manually first (e.g., a finished Clothing prefab).");
            return;
        }

        var template = AssetDatabase.LoadAssetAtPath<GameObject>(templatePath);
        if (template == null || template.GetComponent<Clothing>() == null)
        {
            Debug.LogError($"Template prefab does not contain Clothing component: {templatePath}");
            return;
        }

        // Try to find a material used by the template to duplicate per clothing
        var templateRenderer = template.GetComponentInChildren<Renderer>();
        Material templateMat = templateRenderer != null ? templateRenderer.sharedMaterial : null;
        string templateMatPath = templateMat != null ? AssetDatabase.GetAssetPath(templateMat) : null;

        foreach (var kv in map)
        {
            var type = kv.Key;
            var tokens = kv.Value;

            // Skip already existing prefab with this name pattern
            string prefabName = $"{type}_{(gender == Gender.Female ? "Female" : "Male")}.prefab";
            string existingPath = Path.Combine(targetFolder, prefabName).Replace('\\','/');
            if (File.Exists(existingPath))
            {
                Debug.Log($"Skip existing: {existingPath}");
                continue;
            }

            // Duplicate template prefab
            string newPrefabPath = existingPath;
            if (!AssetDatabase.CopyAsset(templatePath, newPrefabPath))
            {
                Debug.LogError($"Failed to copy template to {newPrefabPath}");
                continue;
            }

            // Load the new prefab as GameObject and edit
            var prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);
            var clothing = prefabObj.GetComponent<Clothing>();
            if (clothing == null)
            {
                Debug.LogError($"New prefab missing Clothing component: {newPrefabPath}");
                continue;
            }

            // Assign Clothing fields
            SetClothingFields(prefabObj, clothing, type, gender);

            // Duplicate and assign material (optional if template had a material)
            if (!string.IsNullOrEmpty(templateMatPath))
            {
                // Material name: <Type>_<Gender>.mat
                string matFileName = $"{type}_{(gender == Gender.Female ? "Female" : "Male")}.mat";
                string matCopyPath = Path.Combine(targetFolder, matFileName).Replace('\\','/');
                if (!File.Exists(matCopyPath))
                {
                    if (!AssetDatabase.CopyAsset(templateMatPath, matCopyPath))
                    {
                        Debug.LogWarning($"Failed to copy material for {type}");
                    }
                }
                var newMat = AssetDatabase.LoadAssetAtPath<Material>(matCopyPath);
                if (newMat != null)
                {
                    // Ensure asset name is clean as well: <Type>_<Gender>
                    newMat.name = $"{type}_{(gender == Gender.Female ? "Female" : "Male")}";
                    foreach (var r in prefabObj.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = r.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            materials[i] = newMat;
                        }
                        r.sharedMaterials = materials;
                    }
                }
            }

            // Find and assign FBX (choose _h.fbx if present)
            var fbx = FindFbxForType(fbxFolder, tokens);
            if (fbx != null)
            {
                clothing.GetType().GetField("fbxFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(clothing, fbx);
            }
            else
            {
                Debug.LogWarning($"FBX not found for {type} in {fbxFolder}");
            }

            EditorUtility.SetDirty(prefabObj);
            AssetDatabase.SaveAssets();
            Debug.Log($"Generated: {newPrefabPath}");
        }

        // Delete the initial template prefab used for copy as requested
        if (!string.IsNullOrEmpty(templatePath) && AssetDatabase.LoadAssetAtPath<GameObject>(templatePath) != null)
        {
            AssetDatabase.DeleteAsset(templatePath);
            Debug.Log($"Deleted template prefab: {templatePath}");
        }

        AssetDatabase.Refresh();
        EditorSceneManager.MarkAllScenesDirty();
    }

    private static void SetClothingFields(GameObject prefabObj, Clothing clothing, ClothingType type, Gender gender)
    {
        // Object name: <Type>_<Gender>
        prefabObj.name = $"{type}_{(gender == Gender.Female ? "Female" : "Male")}";

        // ClothingType & TargetGender
        var clothingSO = new SerializedObject(clothing);
        clothingSO.FindProperty("clothingType").enumValueIndex = (int)type;
        clothingSO.FindProperty("targetGender").enumValueIndex = (int)gender;
        clothingSO.ApplyModifiedPropertiesWithoutUndo();

        // isHideChild = true (in Entity base)
        var entity = clothing.GetComponent<Entity>();
        if (entity != null)
        {
            var entitySO = new SerializedObject(entity);
            var hideProp = entitySO.FindProperty("_isHideChild");
            if (hideProp != null)
            {
                hideProp.boolValue = true;
                entitySO.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static GameObject FindFbxForType(string fbxFolder, string[] tokens)
    {
        // Prefer *_h.fbx, then *_m.fbx, then *_l.fbx
        var guids = AssetDatabase.FindAssets("t:GameObject", new[] { fbxFolder });
        var paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToList();

        GameObject pick(string quality)
        {
            foreach (var p in paths)
            {
                if (!p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!p.Contains("_" + quality + ".fbx")) continue;
                bool allMatch = tokens.All(t => p.ToLowerInvariant().Contains(t));
                if (allMatch)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go != null) return go;
                }
            }
            return null;
        }

        return pick("h") ?? pick("m") ?? pick("l");
    }
}


