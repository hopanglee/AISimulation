using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TokyoAreaTxtExporter
{
    [MenuItem("Tools/Area/Export Tokyo Area Structure TXT (from 11.GameDatas)")]
    public static void ExportTokyoAreaStructure()
    {
        try
        {
            // Base paths
            var baseDir = "Assets/11.GameDatas/Area";
            var krTokyoDir = Path.Combine(baseDir, "kr", "도쿄");
            var enTokyoDir = Path.Combine(baseDir, "en", "Tokyo");
            string rootCityDir = null;
            string rootCityName = null;
            if (Directory.Exists(krTokyoDir)) { rootCityDir = krTokyoDir; rootCityName = "도쿄"; }
            else if (Directory.Exists(enTokyoDir)) { rootCityDir = enTokyoDir; rootCityName = "Tokyo"; }
            else
            {
                Debug.LogWarning("[TokyoAreaTxtExporter] Neither KR nor EN Tokyo directory found under Assets/11.GameDatas/Area.");
                return;
            }

            // Collect all info.json files under the city
            var infoFiles = Directory.GetFiles(rootCityDir, "info.json", SearchOption.AllDirectories);
            if (infoFiles == null || infoFiles.Length == 0)
            {
                Debug.LogWarning($"[TokyoAreaTxtExporter] No info.json files found under {rootCityDir}.");
                return;
            }

            // Build full path strings mapping and connected names
            var fullPaths = new List<string>();
            var leafNameToFullPaths = new Dictionary<string, List<string>>();
            var fileToFullPath = new Dictionary<string, string>();
            foreach (var file in infoFiles)
            {
                var dir = Path.GetDirectoryName(file).Replace("\\", "/");
                var rel = dir.Substring(rootCityDir.Replace("\\", "/").Length).TrimStart('/');
                // Create colon-separated path: City:...:Leaf
                var parts = rel.Length > 0 ? rel.Split('/') : new string[0];
                var full = parts.Length > 0 ? (rootCityName + ":" + string.Join(":", parts)) : rootCityName;
                fullPaths.Add(full);
                fileToFullPath[file] = full;
                var leaf = parts.Length > 0 ? parts[parts.Length - 1] : rootCityName;
                if (!leafNameToFullPaths.TryGetValue(leaf, out var list))
                {
                    list = new List<string>();
                    leafNameToFullPaths[leaf] = list;
                }
                list.Add(full);
            }

            fullPaths = fullPaths.Distinct().OrderBy(p => p, System.StringComparer.Ordinal).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# {rootCityName} Area Hierarchy (from 11.GameDatas)");
            // Indented hierarchy by folder structure
            sb.AppendLine(rootCityName);
            AppendHierarchy(sb, rootCityDir, 1);

            // Read connections and print as simple adjacency with short names
            sb.AppendLine();
            sb.AppendLine($"# {rootCityName} Area Connections (Adjacency, short names)");
            var adjacency = new Dictionary<string, SortedSet<string>>(); // aFull -> neighbor leaf names
            foreach (var file in infoFiles)
            {
                string json = null;
                try { json = File.ReadAllText(file, Encoding.UTF8); }
                catch { continue; }

                var connectedNames = ExtractConnectedAreaNames(json);
                if (connectedNames == null || connectedNames.Count == 0) continue;

                var aFull = fileToFullPath[file];
                if (!adjacency.TryGetValue(aFull, out var set))
                {
                    set = new SortedSet<string>(System.StringComparer.Ordinal);
                    adjacency[aFull] = set;
                }
                foreach (var name in connectedNames)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    set.Add(name);
                }
            }

            foreach (var aFull in adjacency.Keys.OrderBy(k => k, System.StringComparer.Ordinal))
            {
                var shortA = GetShortName(aFull, 2); // last 2 segments for context
                var neighbors = adjacency[aFull];
                sb.AppendLine($"{shortA} <-> {string.Join(", ", neighbors)}");
            }

            // Ensure output directory exists
            var outDir = "Assets/11.GameDatas";
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            var outPath = Path.Combine(outDir, "tokyo_area_structure.txt");
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[TokyoAreaTxtExporter] Exported Tokyo area structure: {outPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TokyoAreaTxtExporter] Export failed: {ex.Message}");
        }
    }

    private static void AppendHierarchy(StringBuilder sb, string dir, int depth)
    {
        try
        {
            var childDirs = Directory.GetDirectories(dir);
            System.Array.Sort(childDirs, (a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), System.StringComparison.Ordinal));
            foreach (var child in childDirs)
            {
                var name = Path.GetFileName(child);
                sb.Append(' ', depth * 2);
                sb.AppendLine(name);
                AppendHierarchy(sb, child, depth + 1);
            }
        }
        catch { /* ignore */ }
    }

    // Extracts connected_areas string[] from info.json without strict schema
    private static List<string> ExtractConnectedAreaNames(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            // very small ad-hoc parser to avoid dependencies
            // looks for "connected_areas": [ ... ] and pulls string elements
            var list = new List<string>();
            var key = "\"connected_areas\"";
            var idx = json.IndexOf(key);
            if (idx < 0) return list;
            idx = json.IndexOf('[', idx);
            if (idx < 0) return list;
            var end = json.IndexOf(']', idx);
            if (end < 0) return list;
            var arr = json.Substring(idx + 1, end - idx - 1);
            // split by commas, extract quoted tokens
            int i = 0;
            while (i < arr.Length)
            {
                while (i < arr.Length && char.IsWhiteSpace(arr[i])) i++;
                if (i >= arr.Length) break;
                if (arr[i] == '"')
                {
                    int j = i + 1;
                    var sb = new StringBuilder();
                    while (j < arr.Length)
                    {
                        if (arr[j] == '"' && arr[j - 1] != '\\') break;
                        sb.Append(arr[j]);
                        j++;
                    }
                    list.Add(sb.ToString());
                    i = j + 1;
                }
                else
                {
                    // skip until next comma/quote
                    while (i < arr.Length && arr[i] != ',') i++;
                    if (i < arr.Length) i++;
                }
            }
            return list;
        }
        catch
        {
            return null;
        }
    }

    private static string GetShortName(string fullPath, int keepSegmentsFromEnd)
    {
        if (string.IsNullOrEmpty(fullPath)) return fullPath;
        var parts = fullPath.Split(':');
        if (parts.Length <= keepSegmentsFromEnd)
        {
            return fullPath;
        }
        return string.Join(":", parts.Skip(parts.Length - keepSegmentsFromEnd));
    }
}


