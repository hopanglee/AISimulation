using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;


/// <summary>
/// 이자카야에서 요리와 서빙을 혼자 담당하는 직원 NPC
/// 전용 액션: Move, Cook
/// - Move: key에 해당하는 위치로 이동
/// - Cook: key에 해당하는 prefab을 만들어 손(우선) 또는 인벤토리에 보관
/// </summary>
public class IzakayaWorker : NPC, IPaymentable
{
    [Title("Izakaya Settings")]
    [InfoBox("Cook으로 만들 수 있는 요리 prefab을 key와 함께 등록하세요. key는 에이전트가 사용할 문자열입니다.")]
    [SerializeField] private SerializableDictionary<string, FoodBlock> cookablePrefabs = new();

    [SerializeField] private int cookSimMinutes = 10; // 분 단위 조리 시간 (시뮬레이션 분)

    [Title("Kitchen (Cooking Room)")]
    [InfoBox("조리 전 이동할 조리실 위치를 지정합니다. Transform이 지정되면 해당 위치로, 없으면 Location Key로 이동합니다.")]
    [SerializeField] private Transform kitchenTransform; // 조리실 위치(Transform)
    [SerializeField] private string kitchenLocationKey;   // 조리실 위치(Key)

    [Title("Payment Settings")]
    [SerializeField, TableList]
    private List<PriceItem> priceList = new List<PriceItem>();

    [SerializeField, ReadOnly]
    private int totalRevenue = 0; // 총 수익

    List<PriceItem> IPaymentable.priceList { get => priceList; set => priceList = value; }
    int IPaymentable.totalRevenue { get => totalRevenue; set => totalRevenue = value; }

    /// <summary>
    /// 이자카야 전용 액션
    /// </summary>
    public struct IzakayaAction : INPCAction
    {
        public NPCActionType ActionName { get; private set; }
        public string Description { get; private set; }

        private IzakayaAction(NPCActionType actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        public static readonly IzakayaAction Move = new(NPCActionType.Move, "특정 위치로 이동");
        public static readonly IzakayaAction Cook = new(NPCActionType.Cook, "지정된 요리를 조리하여 손 또는 인벤토리에 보관");
        public static readonly IzakayaAction Payment = new(NPCActionType.Payment, "결제 처리");

        public override string ToString() => ActionName.ToString();
        public override bool Equals(object obj) => obj is IzakayaAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(IzakayaAction left, IzakayaAction right) => left.Equals(right);
        public static bool operator !=(IzakayaAction left, IzakayaAction right) => !left.Equals(right);
    }

    protected override void InitializeActionHandlers()
    {
        base.InitializeActionHandlers();
        RegisterActionHandler(IzakayaAction.Move, HandleMove);
        RegisterActionHandler(IzakayaAction.Cook, HandleCook);
        RegisterActionHandler(IzakayaAction.Payment, HandlePayment);
    }

    private async UniTask HandleMove(Dictionary<string, object> parameters)
    {
        try
        {
            if (parameters == null || parameters.Count == 0)
            {
                Debug.LogWarning($"[{Name}] Move: 이동할 위치가 지정되지 않았습니다.");
                return;
            }

            string locationKey = parameters["target_key"]?.ToString();
            if (string.IsNullOrEmpty(locationKey))
            {
                Debug.LogWarning($"[{Name}] Move: 유효하지 않은 위치 키입니다.");
                return;
            }

            Debug.Log($"{locationKey}로 이동합니다.");
            var token = currentActionCancellation != null ? currentActionCancellation.Token : CancellationToken.None;
            await MoveToLocationAsync(locationKey, token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"<color=green>[{Name}] HandleMove 취소됨</color>");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] HandleMove 오류: {ex.Message}");
        }
    }

    private async UniTask HandleCook(Dictionary<string, object> parameters)
    {
        try
        {
            var token = currentActionCancellation != null ? currentActionCancellation.Token : CancellationToken.None;

            if (parameters == null || parameters.Count == 0)
            {
                Debug.LogWarning($"[{Name}] Cook: 조리할 key가 없습니다.");
                return;
            }

            string dishKey = parameters["target_key"]?.ToString();
            if (string.IsNullOrEmpty(dishKey))
            {
                Debug.LogWarning($"[{Name}] Cook: 유효하지 않은 key입니다.");
                return;
            }

            if (!cookablePrefabs.ContainsKey(dishKey) || cookablePrefabs[dishKey] == null)
            {
                Debug.Log($"{dishKey}는(은) 조리 목록에 없습니다.");
                await SimDelay.DelaySimMinutes(1, token);
                return;
            }

            // 조리실로 먼저 이동
            await MoveToKitchenAsync(token);

            // 조리 시작
            var bubble = activityBubbleUI;
            try
            {
                if (bubble != null)
                {
                    //bubble.SetFollowTarget(transform);
                    bubble.Show($"{dishKey} 조리 중", 0);
                }
                Debug.Log($"{dishKey}를 조리합니다.");
                await SimDelay.DelaySimMinutes(cookSimMinutes, token);
            }
            finally
            {
                if (bubble != null) bubble.Hide();
            }

            // 프리팹 인스턴스화 (OnEnable 전에 curLocation을 설정하기 위해 비활성 부모 아래에서 생성)
            var prefab = cookablePrefabs[dishKey];
            Vector3 spawnPos = kitchenTransform != null ? kitchenTransform.position : transform.position;

            // 임시 비활성 부모 생성
            GameObject tempParent = new GameObject("_SpawnBuffer_TempParent");
            tempParent.SetActive(false);

            // 비활성 부모 아래에서 인스턴스 생성 (활성 상태는 prefab의 activeSelf를 따르지만, hierarchy상 비활성이라 OnEnable 호출되지 않음)
            GameObject cookedGo = Instantiate(prefab.gameObject, spawnPos, Quaternion.identity, tempParent.transform);
            FoodBlock cookedFood = cookedGo.GetComponent<FoodBlock>();

            // 이름 유지 및 curLocation 선설정 (OnEnable 전에 등록 방지)
            cookedFood.Name = prefab.Name;
            if (curLocation != null)
                cookedFood.curLocation = curLocation;

            // 부모 해제 후 활성화되어 OnEnable 호출됨 (이미 curLocation 설정됨)
            cookedGo.transform.SetParent(null);
            cookedGo.SetActive(true);
            Destroy(tempParent);

            // 손(우선) 또는 인벤토리에 보관 시도
            bool picked = false;
            try
            {
                var bubble2 = activityBubbleUI;
                if (bubble2 != null)
                {
                    //bubble2.SetFollowTarget(transform);
                    bubble2.Show($"{dishKey} 담는 중", 0);
                }
                var pick = PickUp(cookedFood);
                picked = pick.Item1;
            }
            finally
            {
                var bubble2 = activityBubbleUI;
                if (bubble2 != null) bubble2.Hide();
            }
            if (!picked)
            {
                // PickUp 실패 시 현재 위치에 두기 (curLocation은 이미 설정됨)
                Debug.Log($"손과 인벤토리가 가득합니다. {dishKey}를 자리에 두었습니다.");
                await SimDelay.DelaySimMinutes(1, token);
                return;
            }

            Debug.Log($"{dishKey} 준비 완료.");
            await SimDelay.DelaySimMinutes(1, token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"<color=green>[{Name}] HandleCook 취소됨</color>");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] HandleCook 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 결제 처리 액션 핸들러
    /// </summary>
    protected virtual async UniTask HandlePayment(Dictionary<string, object> parameters)
    {
        try
        {
            if (parameters == null || parameters.Count == 0)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 매개변수가 없습니다.");
                ShowSpeech("결제할 아이템을 알려주세요.");
                throw new InvalidOperationException("결제할 아이템 매개변수가 없습니다.");
            }

            string itemName = parameters["item_name"]?.ToString();
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 아이템 이름이 없습니다.");
                ShowSpeech("결제할 아이템을 알려주세요.");
                throw new InvalidOperationException("결제할 아이템 이름이 없습니다.");
            }

            // 가격표에서 아이템 찾기
            PriceItem priceItem = FindPriceItem(itemName);
            if (priceItem == null)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: '{itemName}' 아이템을 찾을 수 없습니다.");
                ShowSpeech($"죄송합니다. '{itemName}' 아이템은 판매하지 않습니다.");
                throw new InvalidOperationException($"'{itemName}' 아이템을 찾을 수 없습니다.");
            }

            Debug.Log($"[{Name}] 결제 처리 시작 - 아이템: {priceItem.itemName}, 가격: {priceItem.price}원");
            ShowSpeech($"{priceItem.itemName} {priceItem.price}원 결제 도와드릴게요.");

            // 보유 금액 0원 특수 처리
            if (Money <= 0)
            {
                Debug.LogWarning($"[{Name}] 결제 실패: 보유 금액 0원. 먼저 돈을 받아야 합니다.");
                ShowSpeech("결제를 위해 먼저 돈을 받아야 합니다.");
                throw new InvalidOperationException("보유 금액이 0원입니다. 먼저 돈을 받아야 합니다.");
            }

            // 보유 금액 체크
            if (Money < priceItem.price)
            {
                Debug.LogWarning($"[{Name}] 결제 실패: 보유 금액 부족 (보유: {Money}원, 필요: {priceItem.price}원)");
                ShowSpeech("죄송합니다. 금액이 부족합니다.");
                throw new InvalidOperationException($"보유 금액이 부족합니다. (보유: {Money}원, 필요: {priceItem.price}원)");
            }

            // 결제 처리 시뮬레이션
            await SimDelay.DelaySimMinutes(2);

            // 결제 성공: 수익 증가, 보유 금액 차감
            Money -= priceItem.price;
            totalRevenue += priceItem.price;

            string paymentReport = $"이자카야 직원 {Name}이 {priceItem.itemName} {priceItem.price}원 결제를 처리했습니다. 현재 보유금: {Money}원, 총 수익: {totalRevenue}원";
            Debug.Log($"[{Name}] 결제 완료: {paymentReport}");
            ShowSpeech("결제가 완료되었습니다. 감사합니다!");

            // AI Agent에 결제 완료 메시지 추가
            if (actionAgent != null)
            {
                string currentTime = GetFormattedCurrentTime();
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    { "time", currentTime },
                    { "itemName", priceItem.itemName },
                    { "price", priceItem.price.ToString() },
                    { "money", Money.ToString() },
                    { "totalRevenue", totalRevenue.ToString() }
                };
                string systemMessage = localizationService.GetLocalizedText("payment_system_message", replacements);
                actionAgent.AddSystemMessage(systemMessage);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"<color=green>[{Name}] HandlePayment 취소됨</color>");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] 결제 처리 중 오류 발생: {ex.Message}");
            throw; // 예외를 다시 던져서 실패로 처리
        }
    }

    /// <summary>
    /// 가격표에서 아이템을 찾습니다.
    /// </summary>
    private PriceItem FindPriceItem(string itemName)
    {
        return priceList.Find(item => item.itemName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 가격표를 가져옵니다.
    /// </summary>
    public List<PriceItem> GetPriceList()
    {
        return new List<PriceItem>(priceList);
    }

    /// <summary>
    /// 특정 아이템의 가격을 가져옵니다.
    /// </summary>
    public int GetItemPrice(string itemName)
    {
        PriceItem item = FindPriceItem(itemName);
        return item?.price ?? 0;
    }

    /// <summary>
    /// 총 수익을 가져옵니다.
    /// </summary>
    public int GetTotalRevenue()
    {
        return totalRevenue;
    }

    /// <summary>
    /// 조리실(Transform 우선, 없으면 LocationKey)로 이동
    /// </summary>
    private async UniTask MoveToKitchenAsync(CancellationToken cancellationToken)
    {
        // Transform이 지정된 경우 해당 위치로 이동
        if (kitchenTransform != null)
        {
            await MoveToPositionAsync(kitchenTransform.position, cancellationToken);
            return;
        }

        // Location Key가 지정된 경우 키 기반 이동
        if (!string.IsNullOrEmpty(kitchenLocationKey))
        {
            await MoveToLocationAsync(kitchenLocationKey, cancellationToken);
            return;
        }

        // 설정이 없으면 이동 생략
    }

    /// <summary>
    /// 지정된 위치로 이동하고 도착/취소/타임아웃 중 하나가 발생할 때까지 대기
    /// </summary>
    private async UniTask MoveToLocationAsync(string locationKey, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool moveStarted = false;

        // 이동 완료 콜백 설정
        System.Action onReachedCallback = () =>
        {
            Debug.Log($"[{Name}] {locationKey}에 도착했습니다.");
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetResult(true);
            }
        };

        // MoveController의 OnReached 이벤트에 콜백 등록
        MoveController.OnReached += onReachedCallback;
        // 취소 콜백 등록
        CancellationTokenRegistration ctr = default;
        if (cancellationToken.CanBeCanceled)
        {
            ctr = cancellationToken.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                try
                {
                    // 이동 중지 및 상태 초기화
                    MoveController.Reset();
                }
                catch (OperationCanceledException)
                {
                    Debug.Log($"<color=green>[{Name}] MoveToLocationAsync 취소됨</color>");
                    throw;
                }
            });
        }

        try
        {
            // 이동 시작
            Move(locationKey);
            moveStarted = true;

            // 이동이 실제로 시작되었는지 확인
            if (!MoveController.isMoving)
            {
                Debug.LogWarning($"[{Name}] 이동이 시작되지 않았습니다: {locationKey}");
                return;
            }

            // 이동 완료까지 대기 (타임아웃과 함께)
            var timeoutTask = Task.Delay(30000, cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            // 도착했는지 확인
            if (tcs.Task.IsCompleted)
            {
                await tcs.Task; // 성공/취소 결과를 전파
            }
            else if (completedTask == timeoutTask)
            {
                Debug.LogWarning($"[{Name}] 이동 타임아웃: {locationKey}");
                MoveController.Reset();
            }
        }
        finally
        {
            // 콜백 정리
            if (moveStarted)
            {
                MoveController.OnReached -= onReachedCallback;
            }
            ctr.Dispose();
        }
    }

    /// <summary>
    /// 지정된 월드 좌표로 이동하고 도착/취소/타임아웃 중 하나가 발생할 때까지 대기
    /// </summary>
    private async UniTask MoveToPositionAsync(Vector3 position, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool moveStarted = false;

        System.Action onReachedCallback = () =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetResult(true);
            }
        };

        MoveController.OnReached += onReachedCallback;
        CancellationTokenRegistration ctr = default;
        if (cancellationToken.CanBeCanceled)
        {
            ctr = cancellationToken.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                try { MoveController.Reset(); } catch (OperationCanceledException) {
                    Debug.Log($"<color=green>[{Name}] MoveToPositionAsync 취소됨</color>");
                    throw;
                }
            });
        }

        try
        {
            MoveToPosition(position);
            moveStarted = true;

            if (!MoveController.isMoving)
            {
                Debug.LogWarning($"[{Name}] 위치 이동이 시작되지 않았습니다: {position}");
                return;
            }

            var timeoutTask = Task.Delay(30000, cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            // 도착했는지 확인
            if (tcs.Task.IsCompleted)
            {
                await tcs.Task; // 성공/취소 결과를 전파
            }
            else if (completedTask == timeoutTask)
            {
                Debug.LogWarning($"[{Name}] 위치 이동 타임아웃: {position}");
                MoveController.Reset();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"<color=green>[{Name}] MoveToPositionAsync 취소됨</color>");
            throw;
        }
        finally
        {
            if (moveStarted)
            {
                MoveController.OnReached -= onReachedCallback;
            }
            ctr.Dispose();
        }
    }

    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음
}


