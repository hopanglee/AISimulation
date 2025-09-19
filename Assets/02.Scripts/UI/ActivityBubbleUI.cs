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
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 2.0f, 0);

    private bool isVisible = false;
    private int remainingSeconds = 0;
    private bool countdownRunning = false;
	private CancellationTokenSource countdownCts;

    private void LateUpdate()
    {
        if (!isVisible || followTarget == null) return;
        transform.position = followTarget.position + worldOffset;
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
			try { countdownCts.Cancel(); } catch {}
			countdownCts.Dispose();
			countdownCts = null;
		}

		remainingSeconds = Mathf.Max(0, totalSeconds);
		isVisible = true;
		gameObject.SetActive(true);

		countdownCts = new CancellationTokenSource();
		if (!countdownRunning)
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
			try { countdownCts.Cancel(); } catch {}
			countdownCts.Dispose();
			countdownCts = null;
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
                        ? $"{activityName}  {remainingSeconds}s"
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


