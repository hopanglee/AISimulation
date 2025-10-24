using System;
using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

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

    #region Time Event Subscription 동시실행


    timeService.SubscribeToTickEvent(tickHub.Publish, int.MinValue); // SimDelay
    timeService.SubscribeToTickEvent(aiMovementTickBridge.OnTick, -1000); // 이번 프레임에 이동 도착 판정해라

    var actors = FindObjectsByType<Actor>(FindObjectsSortMode.None);
    foreach (var actor in actors)
    {
      actor.MoveController.Inititalize();
      timeService.SubscribeToTickEvent(actor.MoveController.OnArrivalTick, -100); // 도착했는지 확인 -> 이거 맨 마지막에 해야함.
      timeService.SubscribeToTimeEvent(actor.MoveController.OnSearchPath, -10);

      if (actor is MainActor mainActor)
      {
        timeService.SubscribeToTimeEvent(mainActor.OnSimulationTimeChanged, 0); // 생일, state 변화 등
        timeService.SubscribeToTickEvent(mainActor.TickMovementAnimation, int.MaxValue);
        timeService.SubscribeToTimeStopStartEvent(mainActor.TickAnimation, int.MaxValue);
        timeService.SubscribeToTimeStopEndEvent(mainActor.TickAnimation, int.MaxValue);

        timeService.SubscribeToTimeStopStartEvent(mainActor.MoveController.Pause, int.MinValue);
        timeService.SubscribeToTimeStopEndEvent(mainActor.MoveController.Resume, int.MinValue);
      }
    }

    timeService.SubscribeToTickEvent(externalEventService.OnTick, -1); // 외부 이벤트

    timeService.SubscribeToTimeEvent(gameService.OnTimeChanged, 0);
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
