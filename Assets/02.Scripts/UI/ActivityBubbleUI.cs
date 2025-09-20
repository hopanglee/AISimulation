using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ActivityBubbleUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text activityText;
    [SerializeField] private Transform followTarget; // 보통 Actor의 머리 Transform 또는 Actor 자체

    [Header("Follow Settings")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 1.0f, 0);

    private bool isVisible = false;
    private int remainingSeconds = 0;
    private bool countdownRunning = false;
    private CancellationTokenSource countdownCts;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (!isVisible || followTarget == null) return;
        if (followTarget == null || mainCamera == null)
            return;

        // Actor의 월드 좌표를 화면 좌표로 변환
        Vector3 actorScreenPosition = ConvertWorldToScreenPosition(followTarget.position + worldOffset);

        // SpeechBubbleUI 오브젝트 자체를 이동 (Canvas가 아닌)
        transform.position = actorScreenPosition;
    }

    /// <summary>
    /// 월드 좌표를 화면 좌표로 변환
    /// </summary>
    private Vector3 ConvertWorldToScreenPosition(Vector3 worldPosition)
    {
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

        // Screen Space - Overlay에서는 Z 좌표가 중요하지 않으므로 X, Y만 사용
        return new Vector3(screenPosition.x, screenPosition.y, 0f);
    }

    public void Show(string activityName, int totalSeconds)
    {
        if (activityText == null)
        {
            Debug.LogWarning("[ActivityBubbleUI] activityText is not assigned.");
            return;
        }

        // 이전 카운트다운이 돌고 있다면 취소
        if (countdownCts != null)
        {
            try { countdownCts.Cancel(); } catch { }
            countdownCts.Dispose();
            countdownCts = null;
        }

        remainingSeconds = Mathf.Max(0, totalSeconds);
        isVisible = true;
        gameObject.SetActive(true);

        countdownCts = new CancellationTokenSource();
        if (!countdownRunning && totalSeconds > 0)
        {
            countdownRunning = true;
            _ = RunCountdownAsync(activityName, countdownCts.Token);
        }
    }

    public void Hide()
    {
        isVisible = false;
        // 카운트다운 즉시 중단
        if (countdownCts != null)
        {
            try { countdownCts.Cancel(); } catch { }
            countdownCts.Dispose();
            countdownCts = null;
            countdownRunning = false;
        }
        gameObject.SetActive(false);
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    private async UniTask RunCountdownAsync(string activityName, CancellationToken token)
    {
        try
        {
            while (isVisible && !token.IsCancellationRequested)
            {
                if (activityText != null)
                {
                    activityText.text = remainingSeconds > 0
                        ? $"{activityName}  {remainingSeconds}초"
                        : activityName;
                }

                if (remainingSeconds <= 0)
                {
                    break;
                }

                // 외부 취소 신호를 전달하여 즉시 중단
                await SimDelay.DelaySimSeconds(1).AttachExternalCancellation(token);
                remainingSeconds--;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ActivityBubbleUI] Countdown stopped: {ex.Message}");
        }
        finally
        {
            countdownRunning = false;
        }
    }
}


