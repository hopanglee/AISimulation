using JetBrains.Annotations;
using UnityEngine;

[DefaultExecutionOrder(-9999)]
public class BootStrapper : MonoBehaviour
{
    void Awake()
    {
        // GameService를 MonoBehaviour로 생성
        var gameServiceGO = new GameObject("GameService");
        var gameService = gameServiceGO.AddComponent<GameService>();
        Services.Provide<IGameService>(gameService);

        Services.Provide<ILocationService>(new LocationService());
        Services.Provide<IPathfindingService>(new PathfindingService());
        Services.Provide<ITimeService>(new TimeManager());
    }
}
