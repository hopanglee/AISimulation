using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-9999)]
public class BootStrapper : MonoBehaviour
{
    async void Awake()
    {
        // Set global log handler to prefix logs with Entity.Name when available
        Debug.unityLogger.logHandler = new PrefixedLogHandler(Debug.unityLogger.logHandler);

        // GameService를 MonoBehaviour로 생성
        var gameServiceGO = new GameObject("GameService");
        var gameService = gameServiceGO.AddComponent<GameService>();
        Services.Provide<IGameService>(gameService);

        // 서비스들 생성 및 초기화
        var locationService = new LocationService();
        var pathfindingService = new PathfindingService();
        var timeService = new TimeManager();
        var actorManager = new ActorManager();
        var externalEventService = new ExternalEventService();
        var localizationService = new LocalizationService();
        var promptService = new PromptService();

        // 언어 설정은 SimulationController에서 처리됨

        // 서비스 등록
        Services.Provide<ILocationService>(locationService);
        Services.Provide<IPathfindingService>(pathfindingService);
        Services.Provide<ITimeService>(timeService);
        Services.Provide<IActorService>(actorManager);
        Services.Provide<IExternalEventService>(externalEventService);
        Services.Provide<ILocalizationService>(localizationService);
        Services.Provide<IPromptService>(promptService);

        // 서비스 초기화
        await locationService.Initialize();
        await pathfindingService.Initialize();
        await timeService.Initialize();
        await actorManager.Initialize();
        await externalEventService.Initialize();
        await localizationService.Initialize();
        await promptService.Initialize();
        await gameService.Initialize();

        Debug.Log("[BootStrapper] 모든 서비스가 성공적으로 초기화되었습니다");
    }
}

// Global log handler that prefixes with [Name] if context is an Entity (or component on same GameObject)
public class PrefixedLogHandler : ILogHandler
{
    private readonly ILogHandler inner;

    public PrefixedLogHandler(ILogHandler inner)
    {
        this.inner = inner;
    }

    public void LogFormat(LogType logType, Object context, string format, params object[] args)
    {
        // Build the message once for filtering and optional prefixing
        string message;
        try { message = string.Format(format, args); }
        catch { message = format; }

        // Ignore noisy engine/library logs
        if (ShouldIgnore(logType, message)) return;

        // Prefix with Entity name when available
        string prefix = GetPrefix(context);
        if (!string.IsNullOrEmpty(prefix))
        {
            message = $"[{prefix}] {message}";
        }

        inner.LogFormat(logType, context, "{0}", message);
    }

    public void LogException(System.Exception exception, Object context)
    {
        inner.LogException(exception, context);
    }

    private string GetPrefix(Object context)
    {
        if (context == null) return null;

        // Direct Entity
        if (context is Entity entity)
        {
            return entity.Name;
        }

        // Component or GameObject that might have Entity
        if (context is Component comp)
        {
            var ent = comp.GetComponent<Entity>();
            if (ent != null) return ent.Name;
        }
        else if (context is GameObject go)
        {
            var ent = go.GetComponent<Entity>();
            if (ent != null) return ent.Name;
        }

        return null;
    }

    private bool ShouldIgnore(LogType logType, string message)
    {
        if (string.IsNullOrEmpty(message)) return false;

        // Suppress A* path debug spam
        if (message.StartsWith("Path Completed")) return true;
        if (message.StartsWith("Path Number")) return true;

        // Suppress frequent shadow atlas warnings
        if (message.StartsWith("Reduced additional punctual light shadows resolution")) return true;

        return false;
    }
}
