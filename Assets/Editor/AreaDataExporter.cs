using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class AreaDataExporter : EditorWindow
{
    public bool isKoreanVersion = false; // 기본값은 영어 버전
    public bool createFoldersForConnectedAreas = true; // connected_areas 폴더 생성 옵션

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

        // 옵션: 씬에 Area 컴포넌트가 없어도 connected_areas에 대해 폴더를 생성
        string toggleLabel = isKoreanVersion
            ? "씬에 없는 connected_areas도 폴더 생성"
            : "Create folders for connected_areas even if no Area exists in scene";
        createFoldersForConnectedAreas = EditorGUILayout.Toggle(toggleLabel, createFoldersForConnectedAreas);

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

        // Key by FULL RELATIVE PATH (e.g., 도쿄/세타가와/…/카미야의 집)
        var areaDataMap = new Dictionary<string, AreaInfo>(System.StringComparer.Ordinal);

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

            // connected_areas에는 실제 Area.connectedAreas만 포함 (하위 Area 자동 포함 제거)

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

            // Compute stable relative path for this Area
            var relPath = BuildAreaRelativePath(area);

            areaDataMap[relPath] = areaInfo;
            Debug.Log(
                $"Area '{areaName}' (relPath='{relPath}') has {areaInfo.connectedAreas.Count} connected areas: {string.Join(", ", areaInfo.connectedAreas)} and {areaInfo.buildings.Count} buildings: {string.Join(", ", areaInfo.buildings)}"
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

        // 정리: 대상 언어 폴더 내부 전체 삭제 후 재생성
        CleanupLanguageFolder(gameDataPath);

        UpdateInfoJsonFiles(gameDataPath, areaDataMap);
        
        // 모든 Area에 대한 폴더 생성 (connected_areas에 있는 것뿐만 아니라 모든 Area)
        CreateAllAreaFolders(gameDataPath, areaDataMap, areas, isKoreanVersion);

        // 옵션에 따라 connected_areas에 대해서도 부모 경로 기준으로 폴더 생성
        if (createFoldersForConnectedAreas)
        {
            CreateFoldersForConnectedAreas(gameDataPath, areaDataMap);
        }

        // 에셋 데이터베이스 갱신
        AssetDatabase.Refresh();
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
            // 폴더의 'basePath' 기준 상대 경로를 키로 사용 (도시/구/동/.../리프)
            var folderDir = Path.GetDirectoryName(infoFile);
            var relFolder = GetRelativePathUnderBase(basePath, folderDir);

            // 우선 전체 경로 기준으로 매칭
            if (areaDataMap.TryGetValue(relFolder, out var areaInfo))
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
                    $"No Area component found for folder: {relFolder} (file: {infoFile})"
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
            // Key is full relative path under basePath
            var relPath = kvp.Key;
            var areaInfo = kvp.Value;
            
            // Create directories by relative path
            var areaFolder = EnsureDirectoriesForRelPath(basePath, relPath);
            
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
        
        // 새 오버로드 사용
        return CreateAreaFolderPath(basePath, targetArea);
    }
    
    /// <summary>
    /// 모든 Area에 대한 폴더를 생성합니다.
    /// </summary>
    private void CreateAllAreaFolders(string basePath, Dictionary<string, AreaInfo> areaDataMap, Area[] areas, bool isKoreanVersion)
    {
        Debug.Log($"Creating folders for all {areas.Length} areas...");
        
        // 이미 존재하는 폴더를 '상대 경로'로 수집
        var existingFolders = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories)
            .Select(folder => GetRelativePathUnderBase(basePath, folder))
            .ToHashSet(System.StringComparer.Ordinal);
        
        int createdCount = 0;
        
        foreach (var area in areas)
        {
            string areaName = isKoreanVersion ? GetKoreanAreaName(area) : GetEnglishAreaName(area);
            
            if (string.IsNullOrEmpty(areaName))
            {
                Debug.LogWarning($"Area {area.name} has no {(isKoreanVersion ? "Korean" : "English")} name, skipping...");
                continue;
            }
            
            var relPath = BuildAreaRelativePath(area);
            Debug.Log($"Checking area: '{areaName}' relPath='{relPath}' - exists: {existingFolders.Contains(relPath)}");
            
            // 이미 폴더가 있으면 건너뛰기 (전체 경로 기준)
            if (!existingFolders.Contains(relPath))
            {
                // 해당 Area에 대한 폴더 생성 (오버로드 사용)
                var folderPath = CreateAreaFolderPath(basePath, area);
                
                // 빈 info.json 파일 생성
                var infoFilePath = Path.Combine(folderPath, "info.json");
                var emptyAreaInfo = new AreaInfo();
                var jsonContent = JsonConvert.SerializeObject(emptyAreaInfo, Formatting.Indented);
                
                File.WriteAllText(infoFilePath, jsonContent);
                Debug.Log($"Created area folder and info.json: {infoFilePath}");
                createdCount++;
            }
        }
        
        if (createdCount > 0)
        {
            Debug.Log($"Created {createdCount} area folders");
        }
    }

    /// <summary>
    /// 각 Area의 connected_areas 목록을 기준으로, 씬에 Area 컴포넌트가 없어도
    /// 부모 Area의 전체 상대 경로 하위에 폴더와 빈 info.json을 생성합니다.
    /// </summary>
    private void CreateFoldersForConnectedAreas(string basePath, Dictionary<string, AreaInfo> areaDataMap)
    {
        Debug.Log("Creating folders for connected_areas under each parent area (path-based)...");

        int createdCount = 0;

        foreach (var kvp in areaDataMap)
        {
            var parentRelPath = kvp.Key; // 예: 도쿄/세타가와/.../카미야의 집
            var parentFolder = EnsureDirectoriesForRelPath(basePath, parentRelPath);

            // 중복 제거 후 처리
            var uniqueChildNames = kvp.Value.connectedAreas.Distinct();
            foreach (var childName in uniqueChildNames)
            {
                if (string.IsNullOrEmpty(childName))
                    continue;

                // 부모 경로 기준으로 하위 폴더 생성
                var childRelPath = parentRelPath + "/" + childName;
                var childFolder = EnsureDirectoriesForRelPath(basePath, childRelPath);

                // 빈 info.json 생성 (없을 때만)
                var infoFilePath = Path.Combine(childFolder, "info.json");
                if (!File.Exists(infoFilePath))
                {
                    var jsonContent = JsonConvert.SerializeObject(new AreaInfo(), Formatting.Indented);
                    File.WriteAllText(infoFilePath, jsonContent);
                    Debug.Log($"Created connected-area folder and info.json: {infoFilePath}");
                    createdCount++;
                }
            }
        }

        if (createdCount > 0)
        {
            Debug.Log($"Created {createdCount} connected-area folders (without Area component)");
        }
    }

    /// <summary>
    /// connected_areas에 있는 Area들에 대한 폴더를 생성합니다.
    /// </summary>
    private void CreateMissingAreaFolders(string basePath, Dictionary<string, AreaInfo> areaDataMap)
    {
        // 이 메서드는 더 이상 사용하지 않음 - CreateAllAreaFolders로 대체됨
        Debug.Log("CreateMissingAreaFolders is deprecated - using CreateAllAreaFolders instead");
    }

    // ---------- Helpers for relative path based logic ----------

    // Build stable relative path like "도쿄/세타가와/.../카미야의 집"
    private string BuildAreaRelativePath(Area targetArea)
    {
        var chain = GetLocationChainNames(targetArea);
        var leaf = isKoreanVersion ? GetKoreanAreaName(targetArea) : GetEnglishAreaName(targetArea);
        if (!string.IsNullOrEmpty(leaf)) chain.Add(leaf);
        var rel = string.Join("/", chain);
        return rel;
    }

    // Collect location chain names from curLocation up to root, plus parent Area container if applicable
    private List<string> GetLocationChainNames(Area targetArea)
    {
        var chain = new List<string>();

        // curLocation chain
        var currentLocation = targetArea != null ? targetArea.curLocation : null;
        while (currentLocation != null)
        {
            var asArea = currentLocation as Area;
            if (asArea != null)
            {
                string locationName = isKoreanVersion ? GetKoreanAreaName(asArea) : GetEnglishAreaName(asArea);
                if (!string.IsNullOrEmpty(locationName))
                {
                    chain.Insert(0, locationName);
                }
            }
            currentLocation = currentLocation.curLocation;
        }

        // if nested under another Area in hierarchy, include direct parent once
        var allAreas = Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
        foreach (var parentArea in allAreas)
        {
            if (targetArea != null && parentArea != targetArea && targetArea.transform.IsChildOf(parentArea.transform))
            {
                string parentAreaName = isKoreanVersion ? GetKoreanAreaName(parentArea) : GetEnglishAreaName(parentArea);
                if (!string.IsNullOrEmpty(parentAreaName) && !chain.Contains(parentAreaName))
                {
                    chain.Add(parentAreaName);
                    Debug.Log($"Add parent Area to chain: '{parentAreaName}'");
                }
                break;
            }
        }

        return chain;
    }

    // Normalize to base-relative path with forward slashes
    private string GetRelativePathUnderBase(string basePath, string fullPath)
    {
        var baseNorm = basePath.Replace("\\", "/");
        var fullNorm = fullPath.Replace("\\", "/");
        if (fullNorm.StartsWith(baseNorm))
        {
            return fullNorm.Substring(baseNorm.Length).TrimStart('/');
        }
        return fullNorm;
    }

    // Ensure directories for a relative path under basePath exist, return full folder path
    private string EnsureDirectoriesForRelPath(string basePath, string relPath)
    {
        var segments = relPath.Replace("\\", "/").Split(new[]{'/'}, System.StringSplitOptions.RemoveEmptyEntries);
        var folderPath = basePath;
        foreach (var seg in segments)
        {
            folderPath = Path.Combine(folderPath, seg);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log($"Created directory: {folderPath}");
            }
        }
        return folderPath;
    }

    // Overload: Create folder path using specific Area (avoids name collisions)
    private string CreateAreaFolderPath(string basePath, Area targetArea)
    {
        if (targetArea == null)
        {
            return basePath;
        }

        var rel = BuildAreaRelativePath(targetArea);
        return EnsureDirectoriesForRelPath(basePath, rel);
    }

    /// <summary>
    /// 대상 언어 폴더(basePath) 내부의 모든 파일/폴더를 삭제합니다(루트 폴더는 유지).
    /// </summary>
    private void CleanupLanguageFolder(string basePath)
    {
        try
        {
            if (!Directory.Exists(basePath))
            {
                return;
            }

            // 하위 디렉터리 모두 삭제
            var subDirs = Directory.GetDirectories(basePath);
            foreach (var dir in subDirs)
            {
                try
                {
                    Directory.Delete(dir, true);
                    Debug.Log($"Deleted directory: {dir}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to delete directory '{dir}': {e.Message}");
                }
            }

            // 루트 하위 파일 모두 삭제(메타 포함)
            var files = Directory.GetFiles(basePath);
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    Debug.Log($"Deleted file: {file}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to delete file '{file}': {e.Message}");
                }
            }

            Debug.Log($"Cleanup completed for: {basePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CleanupLanguageFolder error for '{basePath}': {e.Message}");
        }
    }
}
