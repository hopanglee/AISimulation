using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 호스트클럽 직원 NPC
/// 기본 액션(Wait, Talk, GiveItem)과 Move 액션을 수행할 수 있습니다.
/// </summary>
public class HostClubWorker : NPC, IPaymentable
{
    [Title("Payment Settings")]
    [SerializeField, TableList]
    private List<PriceItem> priceList = new List<PriceItem>();
    
    [SerializeField, ReadOnly]
    private int totalRevenue = 0; // 총 수익
    
    List<PriceItem> IPaymentable.priceList { get => priceList; set => priceList = value; }
    int IPaymentable.totalRevenue { get => totalRevenue; set => totalRevenue = value; }

    /// <summary>
    /// 호스트클럽 직원 전용 액션
    /// </summary>
    public struct HostClubAction : INPCAction
    {
        public NPCActionType ActionName { get; private set; }
        public string Description { get; private set; }

        private HostClubAction(NPCActionType actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        public static readonly HostClubAction Move = new(NPCActionType.Move, "특정 위치로 이동");
        public static readonly HostClubAction Payment = new(NPCActionType.Payment, "서비스비 및 음료비 결제 처리");

        public override string ToString() => ActionName.ToString();
        public override bool Equals(object obj) => obj is HostClubAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(HostClubAction left, HostClubAction right) => left.Equals(right);
        public static bool operator !=(HostClubAction left, HostClubAction right) => !left.Equals(right);
    }

    protected override void InitializeActionHandlers()
    {
        base.InitializeActionHandlers();
        RegisterActionHandler(HostClubAction.Move, HandleMove);
        RegisterActionHandler(HostClubAction.Payment, HandlePayment);
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

            string locationKey = parameters["location_key"]?.ToString();
            if (string.IsNullOrEmpty(locationKey))
            {
                Debug.LogWarning($"[{Name}] Move: 유효하지 않은 위치 키입니다.");
                return;
            }

            var bubble = activityBubbleUI;
            var token = currentActionCancellation != null ? currentActionCancellation.Token : CancellationToken.None;
            try
            {
                if (bubble != null)
                {
                    bubble.SetFollowTarget(transform);
                    bubble.Show($"{locationKey}(으)로 이동 중", 0);
                }
                //ShowSpeech($"{locationKey}로 이동합니다.");
                await MoveToLocationAsync(locationKey, token);
            }
            finally
            {
                if (bubble != null) bubble.Hide();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning($"[{Name}] Move 액션이 취소되었습니다.");
            //ShowSpeech("이동을 취소합니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] HandleMove 오류: {ex.Message}");
        }
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
            tcs.SetResult(true);
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
                catch { }
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

            if (completedTask == timeoutTask) // 타임아웃 발생
            {
                Debug.LogWarning($"[{Name}] 이동 타임아웃: {locationKey}");
                // 타임아웃 시 이동 리셋
                MoveController.Reset();
            }
            else
            {
                // tcs.Task가 완료. 취소/성공 모두 await하여 예외/정상 완료를 전파
                await tcs.Task;
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
    /// 결제 처리 액션 핸들러
    /// </summary>
    protected virtual async UniTask HandlePayment(Dictionary<string, object> parameters)
    {
        try
        {
            if (parameters == null || parameters.Count == 0)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 매개변수가 없습니다.");
                ShowSpeech("결제할 항목을 알려주세요.");
                return;
            }
            
            string itemName = parameters["item_name"]?.ToString();
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 항목 이름이 없습니다.");
                ShowSpeech("결제할 항목을 알려주세요.");
                throw new InvalidOperationException("결제할 항목 이름이 없습니다.");
            }
            
            // 가격표에서 항목 찾기
            PriceItem priceItem = FindPriceItem(itemName);
            if (priceItem == null)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: '{itemName}' 항목을 찾을 수 없습니다.");
                ShowSpeech($"죄송합니다. '{itemName}' 항목은 등록되지 않았습니다.");
                throw new InvalidOperationException($"'{itemName}' 항목을 찾을 수 없습니다.");
            }
            
            Debug.Log($"[{Name}] 결제 처리 시작 - 항목: {priceItem.itemName}, 가격: {priceItem.price}원");
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
            
            string paymentReport = $"호스트클럽 직원 {Name}이 {priceItem.itemName} {priceItem.price}원 결제를 처리했습니다. 현재 보유금: {Money}원, 총 수익: {totalRevenue}원";
            Debug.Log($"[{Name}] 결제 완료: {paymentReport}");
            ShowSpeech("결제가 완료되었습니다. 감사합니다!");
            
            // AI Agent에 결제 완료 메시지 추가
            if (actionAgent != null)
            {
                string currentTime = GetFormattedCurrentTime();
                string systemMessage = $"[{currentTime}] [SYSTEM] 결제 완료 - {priceItem.itemName} {priceItem.price}원, 현재 보유금: {Money}원, 총 수익: {totalRevenue}원";
                actionAgent.AddSystemMessage(systemMessage);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{Name}] 결제 처리 중 오류 발생: {ex.Message}");
            throw; // 예외를 다시 던져서 실패로 처리
        }
    }

    /// <summary>
    /// 가격표에서 항목을 찾습니다.
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
    /// 특정 항목의 가격을 가져옵니다.
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

    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음
}
