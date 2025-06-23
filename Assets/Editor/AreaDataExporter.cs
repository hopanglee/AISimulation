using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class AreaDataExporter : EditorWindow
{
    [System.Serializable]
    public class AreaInfo
    {
        [JsonProperty("connected_areas")]
        public List<string> connectedAreas = new List<string>();
    }

    [MenuItem("Tools/Export Area Connected Areas")]
    public static void ExportAreaConnectedAreas()
    {
        var window = GetWindow<AreaDataExporter>("Area Data Exporter");
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Area Connected Areas Exporter", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Export Connected Areas to GameData"))
        {
            ExportConnectedAreas();
        }

        GUILayout.Space(10);
        GUILayout.Label("This tool will:");
        GUILayout.Label("1. Find all Area components in the scene");
        GUILayout.Label("2. Extract their connectedAreas information");
        GUILayout.Label("3. Update corresponding info.json files in GameData/Area");
    }

    private void ExportConnectedAreas()
    {
        // 씬에서 모든 Area 컴포넌트 찾기
        var areas = FindObjectsOfType<Area>();
        Debug.Log($"Found {areas.Length} Area components in the scene");

        var areaDataMap = new Dictionary<string, AreaInfo>();

        foreach (var area in areas)
        {
            if (string.IsNullOrEmpty(area.locationName))
            {
                Debug.LogWarning($"Area {area.name} has no locationName, skipping...");
                continue;
            }

            var areaInfo = new AreaInfo();

            // connectedAreas에서 locationName 추출
            foreach (var connectedArea in area.connectedAreas)
            {
                if (connectedArea != null && !string.IsNullOrEmpty(connectedArea.locationName))
                {
                    areaInfo.connectedAreas.Add(connectedArea.locationName);
                }
            }

            areaDataMap[area.locationName] = areaInfo;
            Debug.Log(
                $"Area '{area.locationName}' has {areaInfo.connectedAreas.Count} connected areas: {string.Join(", ", areaInfo.connectedAreas)}"
            );
        }

        // GameData/Area 폴더에서 info.json 파일들 찾기
        var gameDataPath = "Assets/11.GameDatas/Area";
        if (!Directory.Exists(gameDataPath))
        {
            Debug.LogError($"GameData path not found: {gameDataPath}");
            return;
        }

        UpdateInfoJsonFiles(gameDataPath, areaDataMap);
    }

    private void UpdateInfoJsonFiles(string basePath, Dictionary<string, AreaInfo> areaDataMap)
    {
        var infoFiles = Directory.GetFiles(basePath, "info.json", SearchOption.AllDirectories);
        Debug.Log($"Found {infoFiles.Length} info.json files");

        int updatedCount = 0;
        int notFoundCount = 0;

        foreach (var infoFile in infoFiles)
        {
            // 폴더명을 Area 이름으로 사용
            var folderName = Path.GetFileName(Path.GetDirectoryName(infoFile));

            if (areaDataMap.TryGetValue(folderName, out var areaInfo))
            {
                // 기존 파일 읽기 (다른 필드가 있을 수 있으므로)
                AreaInfo existingInfo = new AreaInfo();
                if (File.Exists(infoFile))
                {
                    try
                    {
                        var jsonContent = File.ReadAllText(infoFile);
                        existingInfo =
                            JsonConvert.DeserializeObject<AreaInfo>(jsonContent) ?? new AreaInfo();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning(
                            $"Failed to parse existing info.json at {infoFile}: {e.Message}"
                        );
                    }
                }

                // connected_areas만 업데이트
                existingInfo.connectedAreas = areaInfo.connectedAreas;

                // JSON으로 저장
                var updatedJson = JsonConvert.SerializeObject(existingInfo, Formatting.Indented);
                File.WriteAllText(infoFile, updatedJson);

                Debug.Log(
                    $"Updated {infoFile} with {areaInfo.connectedAreas.Count} connected areas"
                );
                updatedCount++;
            }
            else
            {
                Debug.LogWarning(
                    $"No Area component found for folder: {folderName} (file: {infoFile})"
                );
                notFoundCount++;
            }
        }

        Debug.Log(
            $"Export completed: {updatedCount} files updated, {notFoundCount} files not found"
        );
        EditorUtility.DisplayDialog(
            "Export Complete",
            $"Area data export completed!\n{updatedCount} files updated\n{notFoundCount} files not found",
            "OK"
        );
    }
}
