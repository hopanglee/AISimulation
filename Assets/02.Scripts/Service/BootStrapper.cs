using JetBrains.Annotations;
using UnityEngine;

[DefaultExecutionOrder(-9999)]
public class BootStrapper : MonoBehaviour
{
    async void Awake()
    {
        // GameService를 MonoBehaviour로 생성
        var gameServiceGO = new GameObject("GameService");
        var gameService = gameServiceGO.AddComponent<GameService>();
        Services.Provide<IGameService>(gameService);

        // 서비스들 생성 및 초기화
        var locationService = new LocationService();
        var pathfindingService = new PathfindingService();
        var timeService = new TimeManager();

        // 서비스 등록
        Services.Provide<ILocationService>(locationService);
        Services.Provide<IPathfindingService>(pathfindingService);
        Services.Provide<ITimeService>(timeService);

        // 서비스 초기화
        await locationService.Initialize();
        await pathfindingService.Initialize();
        await timeService.Initialize();
        await gameService.Initialize();

        Debug.Log("[BootStrapper] All services initialized successfully");
    }
}
