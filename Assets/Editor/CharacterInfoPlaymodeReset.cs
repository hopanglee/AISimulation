using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Backs up all Characters' folders (including info/ and memory/) at play start and restores on play stop.
// Scope: Assets/11.GameDatas/Character/{ActorName}/**
// Backup path: Assets/11.GameDatas/Backup/Character/{ActorName}/**
// Only runs in the Editor.
public static class CharacterInfoPlaymodeReset
{
    private static bool subscribed;
    private const string CharactersFolderName = "Character";

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
                // Differential snapshot: copy only changed files into backup (fast no-op when unchanged)
                int changedCount = SyncAllCharactersBackup();
                Debug.Log($"[CharacterInfoPlaymodeReset] Snapshot ready. Changed files: {changedCount} (copied/deleted)");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Restore only if any file differs from the snapshot
                bool anyDiff = AnyCharacterDiffersFromBackup();
                if (anyDiff)
                {
                    RestoreAllCharacterInfo();
                    AssetDatabase.Refresh();
                    Debug.Log("[CharacterInfoPlaymodeReset] Differences detected. Restored from snapshot.");
                }
                else
                {
                    Debug.Log("[CharacterInfoPlaymodeReset] No differences detected. Skip restore.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CharacterInfoPlaymodeReset] Error handling playmode change: {ex.Message}");
        }
    }

    private static int SyncAllCharactersBackup()
    {
        var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", "Character");
        if (!Directory.Exists(baseDir)) return 0;
        var backupRoot = Path.Combine(Application.dataPath, "11.GameDatas", "Backup", "Character");
        if (!Directory.Exists(backupRoot)) Directory.CreateDirectory(backupRoot);

        int changedTotal = 0;
        foreach (var characterDir in Directory.GetDirectories(baseDir))
        {
            try
            {
                var characterName = Path.GetFileName(characterDir);
                var backupDir = Path.Combine(backupRoot, characterName);
                changedTotal += SyncDirectory(characterDir, backupDir);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CharacterInfoPlaymodeReset] Snapshot sync failed for '{characterDir}': {ex.Message}");
            }
        }
        return changedTotal;
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

                // Differential restore: only touch files that differ
                RestoreDirectoryFromBackup(backupDir, characterDir);

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

    // === Differential sync helpers ===
    private static int SyncDirectory(string sourceDir, string destDir)
    {
        int changes = 0;
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy/update files from source to dest
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".meta")) continue; // keep Unity metas untouched
            var destFile = Path.Combine(destDir, fileName);

            if (!File.Exists(destFile) || !IsSameByTimeAndSize(file, destFile))
            {
                File.Copy(file, destFile, overwrite: true);
                changes++;
            }
        }

        // Recurse into subdirectories (skip Backup to avoid recursion)
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (string.Equals(name, "Backup", System.StringComparison.OrdinalIgnoreCase)) continue;
            var destSub = Path.Combine(destDir, name);
            changes += SyncDirectory(dir, destSub);
        }

        // Remove extraneous files/dirs from dest that are not in source
        foreach (var destFile in Directory.GetFiles(destDir))
        {
            var name = Path.GetFileName(destFile);
            if (name.EndsWith(".meta")) continue;
            var srcFile = Path.Combine(sourceDir, name);
            if (!File.Exists(srcFile))
            {
                File.SetAttributes(destFile, FileAttributes.Normal);
                File.Delete(destFile);
                changes++;
            }
        }
        foreach (var destSub in Directory.GetDirectories(destDir))
        {
            var name = Path.GetFileName(destSub);
            if (string.Equals(name, "Backup", System.StringComparison.OrdinalIgnoreCase)) continue;
            var srcSub = Path.Combine(sourceDir, name);
            if (!Directory.Exists(srcSub))
            {
                Directory.Delete(destSub, true);
                changes++;
            }
        }

        return changes;
    }

    private static bool AnyCharacterDiffersFromBackup()
    {
        var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", CharactersFolderName);
        var backupRoot = Path.Combine(Application.dataPath, "11.GameDatas", "Backup", CharactersFolderName);
        if (!Directory.Exists(baseDir)) return false;
        if (!Directory.Exists(backupRoot)) return true; // no snapshot available

        foreach (var characterDir in Directory.GetDirectories(baseDir))
        {
            var characterName = Path.GetFileName(characterDir);
            var backupDir = Path.Combine(backupRoot, characterName);
            if (DirectoryDiffers(characterDir, backupDir)) return true;
        }
        // Also, if backup has extra character folders not present in source, that doesn't indicate a change during play, so ignore
        return false;
    }

    private static bool DirectoryDiffers(string sourceDir, string destDir)
    {
        if (!Directory.Exists(destDir))
        {
            // No snapshot for this character -> treat as difference only if there are files to restore
            return Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                .Any(path => !Path.GetFileName(path).EndsWith(".meta"));
        }

        // Check files present/changed
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".meta")) continue;
            var destFile = Path.Combine(destDir, fileName);
            if (!File.Exists(destFile) || !IsSameByTimeAndSize(file, destFile))
            {
                return true;
            }
        }
        // Any extra files in dest indicate snapshot has files not in source -> not a playtime change; ignore for restore decision

        // Recurse into subdirectories (skip Backup)
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (string.Equals(name, "Backup", System.StringComparison.OrdinalIgnoreCase)) continue;
            var destSub = Path.Combine(destDir, name);
            if (DirectoryDiffers(dir, destSub)) return true;
        }

        return false;
    }

    private static bool IsSameByTimeAndSize(string src, string dst)
    {
        try
        {
            var s = new FileInfo(src);
            var d = new FileInfo(dst);
            if (!d.Exists) return false;
            return s.Length == d.Length && s.LastWriteTimeUtc == d.LastWriteTimeUtc;
        }
        catch { return false; }
    }

    private static int RestoreDirectoryFromBackup(string backupDir, string targetDir)
    {
        int changes = 0;
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        // Copy/update files from backup to target
        foreach (var file in Directory.GetFiles(backupDir))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".meta")) continue;
            var targetFile = Path.Combine(targetDir, fileName);
            if (!File.Exists(targetFile) || !IsSameByTimeAndSize(file, targetFile))
            {
                File.Copy(file, targetFile, overwrite: true);
                changes++;
            }
        }

        // Recurse into subdirectories (skip nested Backup)
        foreach (var dir in Directory.GetDirectories(backupDir))
        {
            var name = Path.GetFileName(dir);
            if (string.Equals(name, "Backup", System.StringComparison.OrdinalIgnoreCase)) continue;
            var targetSub = Path.Combine(targetDir, name);
            changes += RestoreDirectoryFromBackup(dir, targetSub);
        }

        // Remove files/dirs that don't exist in backup
        foreach (var targetFile in Directory.GetFiles(targetDir))
        {
            var name = Path.GetFileName(targetFile);
            if (name.EndsWith(".meta")) continue;
            var backupFile = Path.Combine(backupDir, name);
            if (!File.Exists(backupFile))
            {
                File.SetAttributes(targetFile, FileAttributes.Normal);
                File.Delete(targetFile);
                changes++;
            }
        }
        foreach (var targetSub in Directory.GetDirectories(targetDir))
        {
            var name = Path.GetFileName(targetSub);
            if (string.Equals(name, "Backup", System.StringComparison.OrdinalIgnoreCase)) continue;
            var backupSub = Path.Combine(backupDir, name);
            if (!Directory.Exists(backupSub))
            {
                Directory.Delete(targetSub, true);
                changes++;
            }
        }

        return changes;
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


