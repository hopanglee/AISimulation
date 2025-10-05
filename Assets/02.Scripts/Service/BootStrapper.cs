using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

//[DefaultExecutionOrder(-9999)]
public class BootStrapper : MonoBehaviour
{
    void Awake()
    {
        // GameService를 MonoBehaviour로 생성
        var gameServiceGO = new GameObject("GameService");
        var gameService = gameServiceGO.AddComponent<GameService>();

        // 서비스들 생성 및 초기화
        var gptApprovalService = new GPTApprovalService();
        var locationService = new LocationService();
        var pathfindingService = new PathfindingService();
        var timeService = new TimeManager();
        var actorManager = new ActorManager();
        var externalEventService = new ExternalEventService();
        var localizationService = new LocalizationService();

        // 언어 설정은 SimulationController에서 처리됨

        // 서비스 등록
        Services.Provide<IGameService>(gameService);
        Services.Provide<IGPTApprovalService>(gptApprovalService);
        Services.Provide<ILocationService>(locationService);
        Services.Provide<IPathfindingService>(pathfindingService);
        Services.Provide<ITimeService>(timeService);
        Services.Provide<IActorService>(actorManager);
        Services.Provide<IExternalEventService>(externalEventService);
        Services.Provide<ILocalizationService>(localizationService);
        Debug.Log("[BootStrapper] 모든 서비스가 성공적으로 등록되었습니다");
        
        // 서비스 초기화
        gameService.Initialize();
        Debug.Log("[BootStrapper] GameService가 성공적으로 초기화되었습니다");
        gptApprovalService.Initialize();
        Debug.Log("[BootStrapper] GPTApprovalService가 성공적으로 초기화되었습니다");
        locationService.Initialize();
        Debug.Log("[BootStrapper] LocationService가 성공적으로 초기화되었습니다");
        pathfindingService.Initialize();
        Debug.Log("[BootStrapper] PathfindingService가 성공적으로 초기화되었습니다");
        timeService.Initialize();
        Debug.Log("[BootStrapper] TimeService가 성공적으로 초기화되었습니다");
        actorManager.Initialize();
        Debug.Log("[BootStrapper] ActorManager가 성공적으로 초기화되었습니다");
        externalEventService.Initialize();
        Debug.Log("[BootStrapper] ExternalEventService가 성공적으로 초기화되었습니다");
        localizationService.Initialize();
        Debug.Log("[BootStrapper] LocalizationService가 성공적으로 초기화되었습니다");

        Debug.Log("[BootStrapper] 모든 서비스가 성공적으로 초기화되었습니다");
    }
}
