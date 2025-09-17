using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class AreaDataExporter : EditorWindow
{
    public bool isKoreanVersion = false; // 기본값은 영어 버전

    [System.Serializable]
    public class AreaInfo
    {
        [JsonProperty("connected_areas")]
        public List<string> connectedAreas = new List<string>();
        
        [JsonProperty("buildings")]
        public List<string> buildings = new List<string>();
    }

    [MenuItem("Tools/Export Area Connected Areas (EN)")]
    public static void ExportAreaConnectedAreasEN()
    {
        var window = GetWindow<AreaDataExporter>("Area Data Exporter (EN)");
        window.isKoreanVersion = false;
        window.Show();
    }

    [MenuItem("Tools/Export Area Connected Areas (KR)")]
    public static void ExportAreaConnectedAreasKR()
    {
        var window = GetWindow<AreaDataExporter>("Area Data Exporter (KR)");
        window.isKoreanVersion = true;
        window.Show();
    }

    private void OnGUI()
    {
        string versionLabel = isKoreanVersion ? "Area Connected Areas Exporter (Korean)" : "Area Connected Areas Exporter (English)";
        GUILayout.Label(versionLabel, EditorStyles.boldLabel);
        GUILayout.Space(10);

        string buttonText = isKoreanVersion ? "Export Connected Areas to GameData (KR)" : "Export Connected Areas to GameData (EN)";
        if (GUILayout.Button(buttonText))
        {
            ExportConnectedAreas();
        }

        GUILayout.Space(10);
        GUILayout.Label("This tool will:");
        GUILayout.Label("1. Find all Area components in the scene");
        GUILayout.Label("2. Extract their connectedAreas information");
        GUILayout.Label("3. Find buildings in each area");
        string folderText = isKoreanVersion ? "4. Update corresponding info.json files in GameData/Area/kr" : "4. Update corresponding info.json files in GameData/Area/en";
        GUILayout.Label(folderText);
    }

    private void ExportConnectedAreas()
    {
        // 씬에서 모든 Area 컴포넌트 찾기
        var areas = Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
        Debug.Log($"Found {areas.Length} Area components in the scene");

        var areaDataMap = new Dictionary<string, AreaInfo>();

        foreach (var area in areas)
        {
            // 한국어/영어 버전에 따라 다른 이름 사용
            string areaName = isKoreanVersion ? GetKoreanAreaName(area) : GetEnglishAreaName(area);
            
            Debug.Log($"Area: {area.name}, {(isKoreanVersion ? "Korean" : "English")} name: '{areaName}'");
            
            if (string.IsNullOrEmpty(areaName))
            {
                Debug.LogWarning($"Area {area.name} has no {(isKoreanVersion ? "Korean" : "English")} name, skipping...");
                continue;
            }

            var areaInfo = new AreaInfo();

            // connectedAreas에서 이름 추출
            foreach (var connectedArea in area.connectedAreas)
            {
                if (connectedArea != null)
                {
                    string connectedAreaName = isKoreanVersion ? GetKoreanAreaName(connectedArea) : GetEnglishAreaName(connectedArea);
                    if (!string.IsNullOrEmpty(connectedAreaName))
                    {
                        areaInfo.connectedAreas.Add(connectedAreaName);
                    }
                }
            }

            // 해당 Area에 있는 Building들 찾기
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                // Building이 현재 Area의 하위에 있는지 확인
                if (IsBuildingInArea(building, area))
                {
                    string buildingName = isKoreanVersion ? GetKoreanBuildingName(building) : GetEnglishBuildingName(building);
                    if (!string.IsNullOrEmpty(buildingName))
                    {
                        areaInfo.buildings.Add(buildingName);
                    }
                }
            }

            areaDataMap[areaName] = areaInfo;
            Debug.Log(
                $"Area '{areaName}' has {areaInfo.connectedAreas.Count} connected areas: {string.Join(", ", areaInfo.connectedAreas)} and {areaInfo.buildings.Count} buildings: {string.Join(", ", areaInfo.buildings)}"
            );
        }

        // GameData/Area 폴더에서 info.json 파일들 찾기
        string folderName = isKoreanVersion ? "kr" : "en";
        var gameDataPath = $"Assets/11.GameDatas/Area/{folderName}";
        
        // 폴더가 없으면 생성
        if (!Directory.Exists(gameDataPath))
        {
            Debug.Log($"Creating directory: {gameDataPath}");
            Directory.CreateDirectory(gameDataPath);
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

        // 기존 파일이 없는 Area들에 대해 새 파일 생성
        if (infoFiles.Length == 0)
        {
            Debug.Log("No existing info.json files found. Creating new files for all areas...");
            CreateNewInfoJsonFiles(basePath, areaDataMap);
        }

        EditorUtility.DisplayDialog(
            "Export Complete",
            $"Area data export completed!\n{updatedCount} files updated\n{notFoundCount} files not found",
            "OK"
        );
    }

    /// <summary>
    /// Area의 한국어 이름을 가져옵니다.
    /// </summary>
    private string GetKoreanAreaName(Area area)
    {
        // Area 클래스의 _locationNameKr 필드를 리플렉션으로 가져오기
        var field = typeof(Area).GetField("_locationNameKr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            var koreanName = field.GetValue(area) as string;
            Debug.Log($"Area {area.name}: _locationNameKr = '{koreanName}'");
            if (!string.IsNullOrEmpty(koreanName))
                return koreanName;
        }
        else
        {
            Debug.LogWarning($"Area {area.name}: _locationNameKr field not found");
        }
        
        // 한국어 이름이 없으면 영어 이름 반환
        var englishName = GetEnglishAreaName(area);
        Debug.Log($"Area {area.name}: Fallback to English name: '{englishName}'");
        return englishName;
    }

    /// <summary>
    /// Area의 영어 이름을 가져옵니다.
    /// </summary>
    private string GetEnglishAreaName(Area area)
    {
        // Area 클래스의 _locationName 필드를 리플렉션으로 가져오기
        var field = typeof(Area).GetField("_locationName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            var englishName = field.GetValue(area) as string;
            Debug.Log($"Area {area.name}: _locationName = '{englishName}'");
            if (!string.IsNullOrEmpty(englishName))
                return englishName;
        }
        else
        {
            Debug.LogWarning($"Area {area.name}: _locationName field not found");
        }
        
        // 영어 이름이 없으면 GameObject 이름 반환
        Debug.Log($"Area {area.name}: Fallback to GameObject name: '{area.gameObject.name}'");
        return area.gameObject.name;
    }

    /// <summary>
    /// Building의 한국어 이름을 가져옵니다.
    /// </summary>
    private string GetKoreanBuildingName(Building building)
    {
        // Building이 Entity를 상속받으므로 Entity의 _nameKr 필드에 접근
        var entityType = typeof(Entity);
        var field = entityType.GetField("_nameKr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        
        if (field != null)
        {
            var koreanName = field.GetValue(building) as string;
            Debug.Log($"Building {building.name}: _nameKr = '{koreanName}'");
            if (!string.IsNullOrEmpty(koreanName))
                return koreanName;
            else 
                Debug.LogWarning($"Building {building.name}: _nameKr is empty");
        }
        else 
        {
            Debug.LogWarning($"Building {building.name}: _nameKr field not found in Entity class");
            // 모든 필드 확인해보기
            var allFields = entityType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Debug.Log($"Entity class has {allFields.Length} private fields:");
            foreach (var f in allFields)
            {
                Debug.Log($"  - {f.Name} ({f.FieldType.Name})");
            }
        }
        
        // 한국어 이름이 없으면 영어 이름 반환
        return building.gameObject.name;
    }

    /// <summary>
    /// Building의 영어 이름을 가져옵니다.
    /// </summary>
    private string GetEnglishBuildingName(Building building)
    {
        // Building이 Entity를 상속받는다면 _name 필드 사용
        var field = building.GetType().GetField("_name", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            var englishName = field.GetValue(building) as string;
            if (!string.IsNullOrEmpty(englishName))
                return englishName;
        }
        
        // 영어 이름이 없으면 GameObject 이름 반환
        return building.gameObject.name;
    }

    /// <summary>
    /// 기존 파일이 없을 때 새로운 info.json 파일들을 생성합니다.
    /// </summary>
    private void CreateNewInfoJsonFiles(string basePath, Dictionary<string, AreaInfo> areaDataMap)
    {
        int createdCount = 0;
        
        foreach (var kvp in areaDataMap)
        {
            var areaName = kvp.Key;
            var areaInfo = kvp.Value;
            
            // Area의 curLocation을 기반으로 폴더 경로 생성
            var areaFolder = CreateAreaFolderPath(basePath, areaName, areaDataMap);
            
            // info.json 파일 생성
            var infoFilePath = Path.Combine(areaFolder, "info.json");
            var jsonContent = JsonConvert.SerializeObject(areaInfo, Formatting.Indented);
            
            File.WriteAllText(infoFilePath, jsonContent);
            Debug.Log($"Created new info.json: {infoFilePath}");
            Debug.Log($"Content: {jsonContent}");
            
            createdCount++;
        }
        
        Debug.Log($"Created {createdCount} new info.json files");
    }

    /// <summary>
    /// Area의 curLocation을 기반으로 폴더 경로를 생성합니다.
    /// </summary>
    private string CreateAreaFolderPath(string basePath, string areaName, Dictionary<string, AreaInfo> areaDataMap)
    {
        // Area 오브젝트를 찾아서 curLocation 확인
        var areas = Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
        Area targetArea = null;
        
        foreach (var area in areas)
        {
            string currentAreaName = isKoreanVersion ? GetKoreanAreaName(area) : GetEnglishAreaName(area);
            if (currentAreaName == areaName)
            {
                targetArea = area;
                break;
            }
        }
        
        if (targetArea == null)
        {
            Debug.LogWarning($"Area '{areaName}' not found, using flat structure");
            var flatFolder = Path.Combine(basePath, areaName);
            if (!Directory.Exists(flatFolder))
            {
                Directory.CreateDirectory(flatFolder);
            }
            return flatFolder;
        }
        
        // curLocation 체인을 따라 폴더 구조 생성
        var folderPath = basePath;
        var locationChain = new List<string>();
        
        // curLocation 체인 수집
        var currentLocation = targetArea.curLocation;
        while (currentLocation != null)
        {
            string locationName = isKoreanVersion ? GetKoreanAreaName(currentLocation as Area) : GetEnglishAreaName(currentLocation as Area);
            if (!string.IsNullOrEmpty(locationName))
            {
                locationChain.Insert(0, locationName); // 앞에 삽입해서 올바른 순서로
            }
            currentLocation = currentLocation.curLocation;
        }
        
        // 폴더 체인 생성
        foreach (var locationName in locationChain)
        {
            folderPath = Path.Combine(folderPath, locationName);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log($"Created directory: {folderPath}");
            }
        }
        
        // 마지막에 Area 이름 폴더 생성
        var finalFolder = Path.Combine(folderPath, areaName);
        if (!Directory.Exists(finalFolder))
        {
            Directory.CreateDirectory(finalFolder);
            Debug.Log($"Created directory: {finalFolder}");
        }
        
        Debug.Log($"Area '{areaName}' folder path: {finalFolder}");
        return finalFolder;
    }
}
