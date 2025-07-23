using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

public class SpeechBubbleUI : MonoBehaviour
{
    public RectTransform rectTransform;
    public TMP_Text speechText; // 또는 TMP_Text
    public Transform targetWorld; // Actor의 머리 Transform
    public Image backgroundImage; // 말풍선 배경

    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (targetWorld != null)
        {
            Vector3 screenPos = mainCam.WorldToScreenPoint(targetWorld.position);
            rectTransform.position = screenPos;
        }
    }

    public void SetStyle(Color bgColor, Color textColor)
    {
        backgroundImage.color = bgColor;
        speechText.color = textColor;
    }

    public void Show(string message, Transform worldTarget, float duration = 2.5f, Color? bgColor = null, Color? textColor = null)
    {
        speechText.text = message;
        targetWorld = worldTarget;
        if (bgColor.HasValue && textColor.HasValue)
            SetStyle(bgColor.Value, textColor.Value);
        gameObject.SetActive(true);
        // Reset alpha to 1 instantly
        if (backgroundImage != null)
        {
            var c = backgroundImage.color;
            c.a = 1f;
            backgroundImage.color = c;
        }
        if (speechText != null)
        {
            var c = speechText.color;
            c.a = 1f;
            speechText.color = c;
        }
        StartCoroutine(HideAfterSeconds(duration));
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        // Fade out using DOTween
        float fadeDuration = 0.5f;
        Sequence seq = DOTween.Sequence();
        if (backgroundImage != null)
            seq.Join(backgroundImage.DOFade(0f, fadeDuration));
        if (speechText != null)
            seq.Join(speechText.DOFade(0f, fadeDuration));
        yield return seq.WaitForCompletion();
        Hide();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        targetWorld = null;
    }
} 