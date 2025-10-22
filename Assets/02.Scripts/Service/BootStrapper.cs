using System;
using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

//[DefaultExecutionOrder(-9999)]
public class BootStrapper : MonoBehaviour
{
  [SerializeField]
  private int randomSeed = 20001114; // 전역 랜덤 시드 (LLMClient 제외 전역 일관성용)

  void Awake()
  {
    // 전역 RNG 시드 고정 (UnityEngine.Random 기반 호출 모두 결정적 동작)
    UnityEngine.Random.InitState(randomSeed);
    // System.Random 전역 결정성은 DeterministicRandom의 기본 시드(12345)로 초기화되어 있음

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
    var tickHub = new TickEventHub();

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
    Services.Provide<ITickEventHub>(tickHub);


    // 서비스 초기화
    gameService.Initialize();

    gptApprovalService.Initialize();

    locationService.Initialize();

    pathfindingService.Initialize();

    timeService.Initialize();

    actorManager.Initialize();

    externalEventService.Initialize();

    localizationService.Initialize();
    tickHub.Initialize();

    // AIMovementSystemGroup을 TimeManager의 Tick에 맞춰 한 번씩만 실행하도록 브릿지 컴포넌트 추가
    var aiMovementTickBridge = this.gameObject.AddComponent<AIMovementTickBridge>();
    aiMovementTickBridge.Initialize();

    #region Time Event Subscription
    timeService.SubscribeToTimeEvent(gameService.OnTimeChanged);

    var actors = FindObjectsByType<Actor>(FindObjectsSortMode.None);
    foreach (var actor in actors)
    {
      if (actor is MainActor mainActor)
      {
        timeService.SubscribeToTimeEvent(mainActor.OnSimulationTimeChanged);
      }

      timeService.SubscribeToTimeEvent(actor.MoveController.OnGameMinuteChanged);
      timeService.SubscribeToTickEvent(actor.MoveController.OnArrivalTick);
    }

    timeService.SubscribeToTickEvent(tickHub.Publish);
    timeService.SubscribeToTickEvent(aiMovementTickBridge.OnTick);

    timeService.SubscribeToTickEvent(externalEventService.OnTick);
    #endregion


    var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var authPath = $"{userPath}/.openai/auth.json";
    if (File.Exists(authPath))
    {
      var json = File.ReadAllText(authPath);
      var auth = JsonConvert.DeserializeObject<Auth>(json);
      var kamiya_apiKey = auth.claude_api_key;
      var hino_apiKey = auth.claude_api_key_sub;
      var wataya_apiKey = auth.claude_api_key_sub2;
      Claude.SetApiKeyForActor("Kamiya", kamiya_apiKey);
      Claude.SetApiKeyForActor("Hino", hino_apiKey);
      Claude.SetApiKeyForActor("Wataya", wataya_apiKey);
    }
    else
      Debug.LogWarning($"No API key in file path : {authPath}");
  }
}
