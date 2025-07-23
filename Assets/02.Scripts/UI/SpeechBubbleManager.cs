using UnityEngine;
using System.Collections.Generic;

public class SpeechBubbleManager : MonoBehaviour
{
    public static SpeechBubbleManager Instance { get; private set; }
    public SpeechBubbleUI speechBubblePrefab;
    public Canvas canvas;

    private List<SpeechBubbleUI> pool = new List<SpeechBubbleUI>();

    void Awake()
    {
        Instance = this;
    }

    public SpeechBubbleUI GetBubble()
    {
        foreach (var bubble in pool)
        {
            if (!bubble.gameObject.activeSelf)
                return bubble;
        }
        var newBubble = Instantiate(speechBubblePrefab, canvas.transform);
        pool.Add(newBubble);
        return newBubble;
    }

    public void ShowSpeech(Transform worldTarget, string message, float duration = 2.5f)
    {
        var bubble = GetBubble();
        bubble.Show(message, worldTarget, duration);
    }

    public void ShowSpeech(Transform worldTarget, string message, float duration, Color bgColor, Color textColor)
    {
        var bubble = GetBubble();
        bubble.Show(message, worldTarget, duration, bgColor, textColor);
    }

    void LateUpdate()
    {
        // 활성화된 말풍선만 모음
        var activeBubbles = pool.FindAll(b => b.gameObject.activeSelf);
        // 스크린 좌표 기준으로 Y축 정렬
        activeBubbles.Sort((a, b) => a.rectTransform.position.y.CompareTo(b.rectTransform.position.y));
        float minDistance = 40f; // 말풍선 간 최소 간격(픽셀)
        for (int i = 1; i < activeBubbles.Count; i++)
        {
            var prev = activeBubbles[i - 1];
            var curr = activeBubbles[i];
            // 겹치면 위로 올림
            if (Mathf.Abs(curr.rectTransform.position.y - prev.rectTransform.position.y) < minDistance)
            {
                var pos = curr.rectTransform.position;
                pos.y = prev.rectTransform.position.y + minDistance;
                curr.rectTransform.position = pos;
            }
        }
    }
} 