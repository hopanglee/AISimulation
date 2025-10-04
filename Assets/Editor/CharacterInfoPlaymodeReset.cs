using System.IO;
using UnityEditor;
using UnityEngine;

// Backs up all Characters' folders (including info/ and memory/) at play start and restores on play stop.
// Scope: Assets/11.GameDatas/Character/{ActorName}/**
// Backup path: Assets/11.GameDatas/Backup/Character/{ActorName}/**
// Only runs in the Editor.
public static class CharacterInfoPlaymodeReset
{
    private static bool subscribed;

    [InitializeOnLoadMethod]
    private static void Init()
    {
        if (subscribed) return;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        subscribed = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        try
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                BackupAllCharacterInfo();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                RestoreAllCharacterInfo();
                // Ensure changes are visible in Editor after restore
                AssetDatabase.Refresh();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CharacterInfoPlaymodeReset] Error handling playmode change: {ex.Message}");
        }
    }

    private static void BackupAllCharacterInfo()
    {
        var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", "Character");
        if (!Directory.Exists(baseDir)) return;
        var backupRoot = Path.Combine(Application.dataPath, "11.GameDatas", "Backup", "Character");
        if (!Directory.Exists(backupRoot)) Directory.CreateDirectory(backupRoot);

        foreach (var characterDir in Directory.GetDirectories(baseDir))
        {
            try
            {
                var characterName = Path.GetFileName(characterDir);
                var backupDir = Path.Combine(backupRoot, characterName);
                if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
                CopyDirectory(characterDir, backupDir);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CharacterInfoPlaymodeReset] Backup failed for '{characterDir}': {ex.Message}");
            }
        }
        Debug.Log("[CharacterInfoPlaymodeReset] Backed up all Character info.json files on play start.");
    }

    private static void RestoreAllCharacterInfo()
    {
        var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", "Character");
        if (!Directory.Exists(baseDir)) return;
        var backupRoot = Path.Combine(Application.dataPath, "11.GameDatas", "Backup", "Character");

        foreach (var characterDir in Directory.GetDirectories(baseDir))
        {
            try
            {
                var characterName = Path.GetFileName(characterDir);
                var backupDir = Path.Combine(backupRoot, characterName);
                if (!Directory.Exists(backupDir)) continue;

                // Clear current contents (keep root folder to preserve folder meta)
                ClearDirectoryContents(characterDir);
                // Restore from backup
                CopyDirectory(backupDir, characterDir);

                // Import root folder to reflect changes
                var rootRel = ToAssetRelativePath(characterDir);
                if (!string.IsNullOrEmpty(rootRel)) AssetDatabase.ImportAsset(rootRel, ImportAssetOptions.ForceSynchronousImport);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CharacterInfoPlaymodeReset] Restore failed for '{characterDir}': {ex.Message}");
            }
        }
        Debug.Log("[CharacterInfoPlaymodeReset] Restored all Character info.json files on play stop.");
    }

    private static string ToAssetRelativePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return null;
        absolutePath = absolutePath.Replace("\\", "/");
        var dataPath = Application.dataPath.Replace("\\", "/");
        if (absolutePath.StartsWith(dataPath))
        {
            return "Assets" + absolutePath.Substring(dataPath.Length);
        }
        return null;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            // Skip Unity .meta files to prevent GUID conflicts
            if (fileName.EndsWith(".meta")) continue;
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            // Avoid copying nested Backup folders to prevent recursive backups
            if (string.Equals(name, "Backup", System.StringComparison.OrdinalIgnoreCase)) continue;
            var destSub = Path.Combine(destDir, name);
            CopyDirectory(dir, destSub);
        }
    }

    private static void ClearDirectoryContents(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        foreach (var sub in Directory.GetDirectories(dir))
        {
            Directory.Delete(sub, true);
        }
    }
}


