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
        
        [JsonProperty("buildings")]
        public List<string> buildings = new List<string>();
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
        GUILayout.Label("3. Find buildings in each area");
        GUILayout.Label("4. Update corresponding info.json files in GameData/Area");
    }

    private void ExportConnectedAreas()
    {
        // 씬에서 모든 Area 컴포넌트 찾기
        var areas = Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
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

            // 해당 Area에 있는 Building들 찾기
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                // Building이 현재 Area의 하위에 있는지 확인
                if (IsBuildingInArea(building, area))
                {
                    areaInfo.buildings.Add(building.name);
                }
            }

            areaDataMap[area.locationName] = areaInfo;
            Debug.Log(
                $"Area '{area.locationName}' has {areaInfo.connectedAreas.Count} connected areas: {string.Join(", ", areaInfo.connectedAreas)} and {areaInfo.buildings.Count} buildings: {string.Join(", ", areaInfo.buildings)}"
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

    /// <summary>
    /// Building이 특정 Area에 속하는지 확인합니다.
    /// Building이 Area의 하위 오브젝트이거나, Area의 범위 내에 있는지 확인합니다.
    /// </summary>
    private bool IsBuildingInArea(Building building, Area area)
    {
        // 방법 1: Building이 Area의 하위 오브젝트인지 확인
        if (building.transform.IsChildOf(area.transform))
        {
            return true;
        }

        return false;
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

                // connected_areas와 buildings 업데이트
                existingInfo.connectedAreas = areaInfo.connectedAreas;
                existingInfo.buildings = areaInfo.buildings;

                // JSON으로 저장
                var updatedJson = JsonConvert.SerializeObject(existingInfo, Formatting.Indented);
                File.WriteAllText(infoFile, updatedJson);

                Debug.Log(
                    $"Updated {infoFile} with {areaInfo.connectedAreas.Count} connected areas and {areaInfo.buildings.Count} buildings"
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
