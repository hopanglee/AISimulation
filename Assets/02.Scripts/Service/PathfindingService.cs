using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;

public interface IPathfindingService : IService
{
    /// <summary>
    /// 특정 위치로 가기 위한 경로를 찾습니다
    /// </summary>
    /// <param name="startArea">시작 Area</param>
    /// <param name="targetLocationKey">목표 위치 키 (locationName 또는 전체 경로)</param>
    /// <returns>경로 (Area들의 리스트)</returns>
    List<Area> FindPathToLocation(Area startArea, string targetLocationKey);

    /// <summary>
    /// Vector3 위치에서 가장 가까운 Area를 찾습니다
    /// </summary>
    /// <param name="position">찾을 위치</param>
    /// <returns>가장 가까운 Area의 locationName</returns>
    string FindNearestArea(Vector3 position);

    /// <summary>
    /// 전체 월드의 Area 정보를 가져옵니다
    /// </summary>
    /// <returns>모든 Area의 정보</returns>
    Dictionary<string, AreaInfo> GetAllAreaInfo();

    /// <summary>
    /// 전체 경로로 Area를 찾습니다
    /// </summary>
    /// <param name="fullPath">전체 경로</param>
    /// <returns>찾은 Area 또는 null</returns>
    Area FindAreaByFullPath(string fullPath);

    /// <summary>
    /// 전체 경로로 AreaInfo를 찾습니다
    /// </summary>
    /// <param name="fullPath">전체 경로</param>
    /// <returns>찾은 AreaInfo 또는 null</returns>
    AreaInfo GetAreaInfoByFullPath(string fullPath);

    /// <summary>
    /// 전체 경로로 AreaInfo를 가져옵니다
    /// </summary>
    /// <returns>전체 경로를 키로 하는 AreaInfo 딕셔너리</returns>
    Dictionary<string, AreaInfo> GetAllAreaInfoByFullPath();

    List<string> AreaPathToLocationStringPath(List<Area> path);
    List<string> AreaPathToLocationNamePath(List<Area> path);
}

[System.Serializable]
public class AreaInfo
{
    public string locationName;
    public string fullPath; // 전체 경로 (LocationToString 결과)
    public List<string> connectedAreas = new List<string>();
    public List<string> connectedAreasFullPath = new List<string>(); // 연결된 Area들의 전체 경로
    public Vector3 centerPosition;
}

public class PathfindingService : IPathfindingService
{
    private Dictionary<string, AreaInfo> allAreas = new Dictionary<string, AreaInfo>();
    private Dictionary<string, AreaInfo> allAreasByFullPath = new Dictionary<string, AreaInfo>(); // 전체 경로로 검색하기 위한 딕셔너리

    public void Initialize()
    {
        LoadAllAreaInfo();
    }

    private void LoadAllAreaInfo()
    {
        allAreas.Clear();
        allAreasByFullPath.Clear();

        // 씬에서 모든 Area 컴포넌트 찾기
        var areas = Object.FindObjectsByType<Area>(FindObjectsSortMode.None);

        foreach (var area in areas)
        {
            if (string.IsNullOrEmpty(area.locationName))
                continue;

            var fullPath = area.LocationToString();
            var areaInfo = new AreaInfo
            {
                locationName = area.locationName,
                fullPath = fullPath,
                centerPosition = area.transform.position,
            };

            // connectedAreas 정보 추가
            foreach (var connectedArea in area.connectedAreas)
            {
                if (connectedArea != null && !string.IsNullOrEmpty(connectedArea.locationName))
                {
                    areaInfo.connectedAreas.Add(connectedArea.locationName);
                    areaInfo.connectedAreasFullPath.Add(connectedArea.LocationToString());
                }
            }

            allAreas[area.locationName] = areaInfo;
            allAreasByFullPath[fullPath] = areaInfo;
        }

        //Debug.Log($"PathfindingService: Loaded {allAreas.Count} areas");
        // Debug.Log($"PathfindingService: Available full paths: {string.Join(", ", allAreasByFullPath.Keys)}");
    }

    public List<Area> FindPathToLocation(Area startArea, string targetLocationKey)
    {
        if (startArea == null || string.IsNullOrEmpty(targetLocationKey) || string.IsNullOrEmpty(startArea.LocationToString()))
            return new List<Area>();

        // If key has scope and contains a segment like '{n}-chome-{n}' or '나카미세도리',
        // cut everything before that segment and use the remainder as targetTail.
        bool hasScope = targetLocationKey.IndexOf(':') >= 0;
        string[] keyTokens = hasScope ? targetLocationKey.Split(':') : new[] { targetLocationKey };
        int pivotIndex = -1;
        for (int i = 0; i < keyTokens.Length; i++)
        {
            var tok = keyTokens[i];
            if (Regex.IsMatch(tok ?? string.Empty, @"^\d+-chome-\d+$") || string.Equals(tok, "나카미세도리", System.StringComparison.Ordinal))
            {
                pivotIndex = i;
                break;
            }
        }
        bool allowTailMatch = hasScope && pivotIndex >= 0;
        string targetTail = allowTailMatch ? string.Join(":", keyTokens, pivotIndex, keyTokens.Length - pivotIndex) : null;
        string tailLeaf = allowTailMatch ? keyTokens[keyTokens.Length - 1] : null;

        // BFS로 최단 경로 찾기
        var queue = new Queue<Area>();
        var visited = new HashSet<Area>();
        var parent = new Dictionary<Area, Area>();

        queue.Enqueue(startArea);
        visited.Add(startArea);

        var locationManager = Services.Get<ILocationService>();
        var buildings = locationManager.GetAllBuildings().Select(b => b.locationName).ToList().Distinct();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // targetLocationKey 매칭 규칙 (조기 성공 방지):
            // 1) 전체 경로 완전 일치만 즉시 성공
            // 2) 스코프 경계 단위로 정규화하여 비교 (':' 토큰 기준 접두 일치만 허용)
            // 3) tail 비교는 현재 노드가 스코프 경계(예: 블록 or 빌딩)일 때만 허용
            // 4) 지역명은 완전 일치만 허용 (부분 문자열 금지)
            bool isTargetFound = false;
            var currentFullPath = current.LocationToString();
            var building = locationManager.GetBuilding(current);

            // 규칙 1: 전체 경로 완전 일치
            if (!string.IsNullOrEmpty(currentFullPath) && currentFullPath == targetLocationKey)
            {
                isTargetFound = true;
            }
            else
            {
                // 현재와 타깃을 스코프 토큰으로 분해
                var currentTokens = string.IsNullOrEmpty(currentFullPath) ? null : currentFullPath.Split(':');
                var targetTokens = string.IsNullOrEmpty(targetLocationKey) ? null : targetLocationKey.Split(':');

                // 규칙 2: 접두 스코프 비교 (토큰 기준). 부분 문자열 포함 금지
                if (currentTokens != null && targetTokens != null)
                {
                    int compareCount = Mathf.Min(currentTokens.Length, targetTokens.Length);
                    bool allPrefixEqual = true;
                    for (int i = 0; i < compareCount; i++)
                    {
                        if (!string.Equals(currentTokens[i], targetTokens[i], System.StringComparison.Ordinal))
                        {
                            allPrefixEqual = false;
                            break;
                        }
                    }

                    // 접두가 완전히 같은 경우에만 매칭으로 간주하되,
                    // 동일 길이면 규칙 1에서 이미 처리됨. 여기서는 타깃이 더 짧아 상위 스코프를 가리킬 때만 허용
                    if (allPrefixEqual && targetTokens.Length < currentTokens.Length)
                    {
                        isTargetFound = true;
                    }
                }

                // 규칙 3: tail 비교는 현재가 스코프 경계일 때만 허용
                if (!isTargetFound && allowTailMatch && !string.IsNullOrEmpty(currentFullPath) && !string.IsNullOrEmpty(targetTail))
                {
                    // 스코프 경계 여부 추정: 현재 노드가 블록(예: \d+-chome-\d+)이거나 빌딩 루트 토큰으로 끝날 때
                    bool isScopeBoundary = false;
                    if (currentTokens != null && currentTokens.Length > 0)
                    {
                        string last = currentTokens[currentTokens.Length - 1];
                        if (Regex.IsMatch(last ?? string.Empty, @"^\d+-chome-\d+$"))
                            isScopeBoundary = true;
                        else if (building != null && string.Equals(last, building.locationName, System.StringComparison.Ordinal))
                            isScopeBoundary = true;
                    }

                    if (isScopeBoundary)
                    {
                        // tail은 반드시 토큰 경계에서 접미로 일치해야 함
                        // currentFullPath가 targetTail로 끝나는지 확인 (대소문자 구분)
                        if (currentFullPath.EndsWith(targetTail, System.StringComparison.Ordinal))
                        {
                            isTargetFound = true;
                        }
                    }


                    if (buildings.Contains(tailLeaf))
                    { // 그냥 빌딩이 찾고싶던거임
                        if (currentFullPath.IndexOf(targetTail) >= 0)
                        {
                            isTargetFound = true;
                        }
                    }
                }

                // 규칙 4: 지역명 완전 일치만 허용
                if (!isTargetFound && current.locationName == targetLocationKey)
                {
                    isTargetFound = true;
                }

                // 빌딩명 비교는 완전 일치만 허용. tailLeaf 부분 일치는 금지
                if (!isTargetFound && building != null && building.locationName == targetLocationKey)
                {
                    isTargetFound = true;
                }


                buildings = locationManager.GetAllBuildings().Select(b => b.locationName).ToList().Distinct();
                if (buildings.Contains(tailLeaf))
                { // 그냥 빌딩 이름만 
                    if (building != null && building.locationName == tailLeaf)
                    {
                        isTargetFound = true;
                    }
                }
            }


            if (isTargetFound)
            {
                // 경로 재구성
                return ReconstructPath(parent, startArea, current);
            }

            // 이웃 탐색: connectedAreas와 toMovePos 키 모두를 사용하되, 중복은 제거
            var neighbors = new HashSet<Area>();
            if (current.connectedAreas != null && current.connectedAreas.Count > 0)
            {
                foreach (var a in current.connectedAreas)
                {
                    if (a != null) neighbors.Add(a);
                }
            }
            if (current.toMovePos != null && current.toMovePos.Keys != null)
            {
                foreach (var a in current.toMovePos.Keys)
                {
                    if (a != null) neighbors.Add(a);
                }
            }

            foreach (var connected in neighbors)
            {
                if (connected == null) continue;
                if (!visited.Contains(connected))
                {
                    visited.Add(connected);
                    parent[connected] = current;
                    queue.Enqueue(connected);
                }
            }
        }

        Debug.LogWarning($"No path found from {startArea.locationName} to {targetLocationKey}");
        return new List<Area>();
    }

    /// <summary>
    /// 전체 경로로 Area를 찾습니다
    /// </summary>
    public Area FindAreaByFullPath(string fullPath)
    {
        if (allAreasByFullPath.TryGetValue(fullPath, out var areaInfo))
        {
            // AreaInfo에서 실제 Area 컴포넌트를 찾기
            var areas = Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
            foreach (var area in areas)
            {
                if (area.locationName == areaInfo.locationName)
                {
                    return area;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 전체 경로로 AreaInfo를 찾습니다
    /// </summary>
    public AreaInfo GetAreaInfoByFullPath(string fullPath)
    {
        allAreasByFullPath.TryGetValue(fullPath, out var areaInfo);
        return areaInfo;
    }

    private List<Area> ReconstructPath(
        Dictionary<Area, Area> parent,
        Area start,
        Area end
    )
    {
        var path = new List<Area>();
        var current = end;

        while (current != start)
        {
            path.Add(current);
            if (!parent.TryGetValue(current, out current))
                break;
        }

        path.Add(start);
        path.Reverse();
        return path;
    }

    public List<string> AreaPathToLocationStringPath(List<Area> path)
    {
        var locationService = Services.Get<ILocationService>();
        return path.Select(area =>
         {
             if (locationService != null)
             {
                 var building = locationService.GetBuilding(area);
                 if (building != null && !string.IsNullOrEmpty(building.locationName))
                 {
                     // Show from building level onward: e.g., "카페 모카하우스:홀"
                     var full = area.LocationToString();
                     if (!string.IsNullOrEmpty(full))
                     {
                         var tokens = full.Split(':');
                         for (int i = 0; i < tokens.Length; i++)
                         {
                             if (string.Equals(tokens[i], building.locationName, System.StringComparison.Ordinal))
                             {
                                 return string.Join(":", tokens, i, tokens.Length - i);
                             }
                         }

                         Debug.LogWarning($"Failed to parse full path: {full} for area: {area.locationName}, building: {building.locationName}");
                     }
                 }
             }
             return area.locationName; // default: area name
         }).ToList();
    }


    public List<string> AreaPathToLocationNamePath(List<Area> path)
    {
        var locationService = Services.Get<ILocationService>();
        return path.Select(area =>
         {
             return area.locationName; // default: area name
         }).ToList();
    }

    public string FindNearestArea(Vector3 position)
    {
        string nearestArea = null;
        float nearestDistance = float.MaxValue;

        foreach (var kvp in allAreas)
        {
            float distance = Vector3.Distance(position, kvp.Value.centerPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestArea = kvp.Key;
            }
        }

        return nearestArea;
    }

    public Dictionary<string, AreaInfo> GetAllAreaInfo()
    {
        return new Dictionary<string, AreaInfo>(allAreas);
    }

    /// <summary>
    /// 전체 경로로 AreaInfo를 가져옵니다
    /// </summary>
    public Dictionary<string, AreaInfo> GetAllAreaInfoByFullPath()
    {
        return new Dictionary<string, AreaInfo>(allAreasByFullPath);
    }
}
