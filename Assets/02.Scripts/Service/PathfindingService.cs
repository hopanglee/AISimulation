using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IPathfindingService : IService
{
    /// <summary>
    /// 특정 위치로 가기 위한 경로를 찾습니다
    /// </summary>
    /// <param name="startArea">시작 Area</param>
    /// <param name="targetLocationKey">목표 위치 키 (locationName 또는 전체 경로)</param>
    /// <returns>경로 (Area들의 리스트)</returns>
    List<string> FindPathToLocation(Area startArea, string targetLocationKey);

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

        Debug.Log($"PathfindingService: Loaded {allAreas.Count} areas");
        Debug.Log($"PathfindingService: Available full paths: {string.Join(", ", allAreasByFullPath.Keys)}");
    }

    public List<string> FindPathToLocation(Area startArea, string targetLocationKey)
    {
        if (string.IsNullOrEmpty(startArea.locationName) || string.IsNullOrEmpty(targetLocationKey))
            return new List<string>();

        // BFS로 최단 경로 찾기
        var queue = new Queue<string>();
        var visited = new HashSet<string>();
        var parent = new Dictionary<string, string>();

        var startLocationName = startArea.locationName;
        queue.Enqueue(startLocationName);
        visited.Add(startLocationName);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // targetLocationKey가 전체 경로인지 locationName인지 확인
            bool isTargetFound = false;
            if (current == targetLocationKey)
            {
                isTargetFound = true;
            }
            else if (allAreasByFullPath.ContainsKey(targetLocationKey) && 
                     allAreasByFullPath[targetLocationKey].locationName == current)
            {
                isTargetFound = true;
            }

            if (isTargetFound)
            {
                // 경로 재구성
                return ReconstructPath(parent, startLocationName, current);
            }

            if (allAreas.TryGetValue(current, out var currentAreaInfo))
            {
                foreach (var connected in currentAreaInfo.connectedAreas)
                {
                    if (!visited.Contains(connected))
                    {
                        visited.Add(connected);
                        parent[connected] = current;
                        queue.Enqueue(connected);
                    }
                }
            }
        }

        Debug.LogWarning($"No path found from {startArea.locationName} to {targetLocationKey}");
        return new List<string>();
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

    private List<string> ReconstructPath(
        Dictionary<string, string> parent,
        string start,
        string end
    )
    {
        var path = new List<string>();
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
