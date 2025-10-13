using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;

public static class LocationMemoryWorldBuilder
{
    [MenuItem("Tools/Area/Build Location Memories (World → Characters)")]
    public static void BuildLocationMemories()
    {
        try
        {
            // 1) Gather world data from 11.GameDatas/Area (KR preferred, fallback EN)
            var baseDir = "Assets/11.GameDatas/Area";
            var krDir = Path.Combine(baseDir, "kr");
            var enDir = Path.Combine(baseDir, "en");
            string rootDir = null;
            if (Directory.Exists(krDir)) rootDir = krDir;
            else if (Directory.Exists(enDir)) rootDir = enDir;
            else
            {
                Debug.LogError("[LocationMemoryWorldBuilder] No Area directory found under Assets/11.GameDatas/Area (kr or en)");
                return;
            }

            // Find all info.json files
            var infoFiles = Directory.GetFiles(rootDir, "info.json", SearchOption.AllDirectories);
            if (infoFiles == null || infoFiles.Length == 0)
            {
                Debug.LogWarning($"[LocationMemoryWorldBuilder] No info.json found under {rootDir}");
                return;
            }

            // Build path mappings
            var fileToFullPath = new Dictionary<string, string>();
            var leafNameToFullPaths = new Dictionary<string, List<string>>();
            foreach (var file in infoFiles)
            {
                var dir = Path.GetDirectoryName(file).Replace("\\", "/");
                var rel = dir.Substring(rootDir.Replace("\\", "/").Length).TrimStart('/');
                var parts = rel.Length > 0 ? rel.Split('/') : new string[0];
                var full = parts.Length > 0 ? string.Join(":", parts) : string.Empty; // exclude city prefix for memory keys
                fileToFullPath[file] = full;
                var leaf = parts.Length > 0 ? parts[parts.Length - 1] : full;
                if (!leafNameToFullPaths.TryGetValue(leaf, out var list))
                {
                    list = new List<string>();
                    leafNameToFullPaths[leaf] = list;
                }
                list.Add(full);
            }

            // Build memory dictionary: key = full path (without language/city prefix), value = LocationData-like object
            var memoryRoot = new JObject();
            foreach (var file in infoFiles)
            {
                string json;
                try { json = File.ReadAllText(file, Encoding.UTF8); }
                catch { continue; }

                var fullPath = fileToFullPath[file];
                if (string.IsNullOrEmpty(fullPath)) continue; // skip city root

                var buildings = ExtractArrayOfStrings(json, "buildings");
                var connected = ExtractArrayOfStrings(json, "connected_areas");
                buildings = SortList(MakeUniqueWithSuffix(buildings ?? new List<string>()));
                connected = SortList(MakeUniqueWithSuffix(connected ?? new List<string>()));

                var obj = new JObject
                {
                    ["items"] = new JArray(),
                    ["props"] = new JArray(),
                    ["actors"] = new JArray(),
                    ["buildings"] = new JArray(buildings),
                    ["connectedAreas"] = new JArray(connected),
                    ["lastSeen"] = GameTimeToIsoString(2024, 11, 14, 0, 0)
                };

                memoryRoot[fullPath] = obj;
            }

            // 2) Write to characters: 카미야, 히노
            WriteMemoryForCharacter("카미야", memoryRoot);
            WriteMemoryForCharacter("히노", memoryRoot);

            AssetDatabase.Refresh();
            Debug.Log("[LocationMemoryWorldBuilder] Built location memories for 카미야, 히노.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocationMemoryWorldBuilder] Build failed: {ex.Message}");
        }
    }

    [MenuItem("Tools/Area/Build Location Memories From Scene (scan Entities)")]
    public static void BuildLocationMemoriesFromScene()
    {
        try
        {
            // 1) Find all Areas in scene (active only)
            var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None)
                .Where(a => a != null && a.gameObject.activeInHierarchy && a.enabled)
                .ToArray();
            if (areas == null || areas.Length == 0)
            {
                Debug.LogWarning("[LocationMemoryWorldBuilder] No Area components found in scene.");
                return;
            }

            // Preload entity arrays for membership check (active only)
            var items = (UnityEngine.Object.FindObjectsByType<Item>(FindObjectsSortMode.None) ?? System.Array.Empty<Item>())
                .Where(e => e != null && e.gameObject.activeInHierarchy && e.enabled)
                .ToArray();
            var props = (UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsSortMode.None) ?? System.Array.Empty<Prop>())
                .Where(e => e != null && e.gameObject.activeInHierarchy && e.enabled)
                .ToArray();
            var actors = (UnityEngine.Object.FindObjectsByType<Actor>(FindObjectsSortMode.None) ?? System.Array.Empty<Actor>())
                .Where(e => e != null && e.gameObject.activeInHierarchy && e.enabled)
                .ToArray();
            var buildings = (UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None) ?? System.Array.Empty<Building>())
                .Where(e => e != null && e.gameObject.activeInHierarchy && e.enabled)
                .ToArray();

            // Build memory
            var memoryRoot = new JObject();
            foreach (var area in areas)
            {
                if (area == null || string.IsNullOrEmpty(area.locationName)) continue;

                // Use KR full path for uniqueness
                var key = BuildFullPathKr(area);
                if (string.IsNullOrEmpty(key)) continue;

                // Collect child membership by transform hierarchy
                var areaTf = area.transform;
                // Exclude entities that live under nested child Areas (only include directly-owned members)
                var childAreaTfs = areas
                    .Where(a => a != null && a != area && a.transform.IsChildOf(areaTf))
                    .Select(a => a.transform)
                    .ToArray();

                bool IsOwnedByThisArea(Transform t)
                {
                    if (t == null) return false;
                    if (!t.IsChildOf(areaTf)) return false;
                    // Exclude anything under a child Area transform
                    foreach (var cat in childAreaTfs)
                    {
                        if (t.IsChildOf(cat)) return false;
                    }
                    return true;
                }

                var areaItems = items.Where(e => e != null && IsOwnedByThisArea(e.transform)).Select(GetEntityKrName).Where(n => !string.IsNullOrEmpty(n)).ToList();
                var areaProps = props.Where(e => e != null && IsOwnedByThisArea(e.transform)).Select(GetEntityKrName).Where(n => !string.IsNullOrEmpty(n)).ToList();
                var areaActors = actors.Where(e => e != null && IsOwnedByThisArea(e.transform)).Select(GetEntityKrName).Where(n => !string.IsNullOrEmpty(n)).ToList();
                var areaBuildings = buildings.Where(e => e != null && IsOwnedByThisArea(e.transform)).Select(GetEntityKrName).Where(n => !string.IsNullOrEmpty(n)).ToList();
                var connected = (area.connectedAreas ?? new List<Area>())
                    .Where(a => a != null && a.gameObject.activeInHierarchy && a.enabled && !string.IsNullOrEmpty(a.locationName))
                    .Select(GetAreaKrName)
                    .ToList();

                // Make names unique with suffixes instead of dropping duplicates
                areaItems = SortList(MakeUniqueWithSuffix(areaItems));
                areaProps = SortList(MakeUniqueWithSuffix(areaProps));
                areaActors = SortList(MakeUniqueWithSuffix(areaActors));
                areaBuildings = SortList(MakeUniqueWithSuffix(areaBuildings));
                connected = SortList(MakeUniqueWithSuffix(connected));

                // Skip areas with no connections at all
                if (connected.Count == 0) continue;

                var obj = new JObject
                {
                    ["items"] = new JArray(areaItems),
                    ["props"] = new JArray(areaProps),
                    ["actors"] = new JArray(areaActors),
                    ["buildings"] = new JArray(areaBuildings),
                    ["connectedAreas"] = new JArray(connected),
                    ["lastSeen"] = GameTimeToIsoString(2024, 11, 14, 0, 0)
                };

                memoryRoot[key] = obj;
            }

            // 2) Write to characters
            WriteMemoryForCharacter("카미야", memoryRoot);
            WriteMemoryForCharacter("히노", memoryRoot);
            WriteMemoryForCharacter("와타야", memoryRoot);

            AssetDatabase.Refresh();
            Debug.Log("[LocationMemoryWorldBuilder] Built location memories from scene for 카미야, 히노.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocationMemoryWorldBuilder] Build from scene failed: {ex.Message}");
        }
    }

    private static string BuildFullPathKr(Area area)
    {
        if (area == null) return null;
        var names = new List<string>();
        // climb ancestors first to collect in reverse
        var stack = new Stack<Area>();
        var curLoc = area as ILocation;
        while (curLoc != null)
        {
            if (curLoc is Area a) stack.Push(a);
            curLoc = curLoc.curLocation;
        }
        while (stack.Count > 0)
        {
            var a = stack.Pop();
            var n = GetAreaKrName(a);
            if (!string.IsNullOrEmpty(n)) names.Add(n);
        }
        return names.Count > 0 ? string.Join(":", names) : null;
    }

    private static string GetAreaKrName(Area a)
    {
        if (a == null) return null;
        try
        {
            var fi = typeof(Area).GetField("_locationNameKr", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null)
            {
                var val = fi.GetValue(a) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { }
        // fallback to localized name (depends on current language)
        try { var ln = a.GetLocalizedName(); if (!string.IsNullOrEmpty(ln)) return ln; } catch { }
        return a.locationName;
    }

    private static string GetEntityKrName(Entity e)
    {
        if (e == null) return null;
        try
        {
            var fi = typeof(Entity).GetField("_nameKr", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null)
            {
                var val = fi.GetValue(e) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { }
        // fallback to localized name
        try { var ln = e.GetLocalizedName(); if (!string.IsNullOrEmpty(ln)) return ln; } catch { }
        return e.Name;
    }

    private static List<string> MakeUniqueWithSuffix(IEnumerable<string> names)
    {
        var result = new List<string>();
        var counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var raw in names ?? Array.Empty<string>())
        {
            var baseName = raw ?? string.Empty;
            if (!counts.TryGetValue(baseName, out var c))
            {
                counts[baseName] = 1;
                result.Add(baseName);
            }
            else
            {
                result.Add($"{baseName}_{c}");
                counts[baseName] = c + 1;
            }
        }
        return result;
    }

    private static List<string> SortList(IEnumerable<string> names)
    {
        return (names ?? Array.Empty<string>()).OrderBy(s => s, System.StringComparer.Ordinal).ToList();
    }

    private static void WriteMemoryForCharacter(string characterName, JObject memoryRoot)
    {
        var basePath = Path.Combine(Application.dataPath, "11.GameDatas", "Character", characterName, "memory", "location");
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
        var outPath = Path.Combine(basePath, "location_memories.json");
        File.WriteAllText(outPath, memoryRoot.ToString(Formatting.Indented), Encoding.UTF8);
    }

    private static string GameTimeToIsoString(int year, int month, int day, int hour, int minute)
    {
        var gt = new GameTime(year, month, day, hour, minute);
        return gt.ToIsoString();
    }

    private static List<string> ExtractArrayOfStrings(string json, string keyName)
    {
        try
        {
            var jo = JObject.Parse(json);
            var token = jo[keyName];
            if (token is JArray arr)
            {
                return arr.Select(t => t.Type == JTokenType.String ? t.Value<string>() : null)
                          .Where(s => !string.IsNullOrEmpty(s))
                          .Distinct()
                          .ToList();
            }
        }
        catch { }
        return new List<string>();
    }
}


