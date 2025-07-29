using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

public class SpeechBubbleUI : MonoBehaviour
{
    public TMP_Text speechText;
    public Image backgroundImage;

    [Header("Speech Bubble Style")]
    public Color speechBgColor = Color.black;
    public Color speechTextColor = Color.white;

    private Coroutine hideCoroutine;
    private Sequence fadeSequence;
    private Camera mainCamera;
    private Canvas canvas;
    private Transform canvasParent;

    private void Awake()
    {
        // 초기 상태는 숨김
        gameObject.SetActive(false);
        
        // 기본 스타일 설정
        SetStyle(speechBgColor, speechTextColor);
        
        // 메인 카메라 찾기
        mainCamera = Camera.main;
        
        // Canvas와 그 부모 찾기
        canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvasParent = canvas.transform.parent;
            
            // Canvas 설정
            SetupCanvas();
        }
    }

    private void SetupCanvas()
    {
       
    }

    private void LateUpdate()
    {
        // Canvas의 부모를 카메라에 항상 직각으로 보이도록 회전 조정
        if (mainCamera != null && canvasParent != null && gameObject.activeInHierarchy)
        {
            // Canvas의 부모 오브젝트를 카메라 방향으로 회전
            canvasParent.rotation = mainCamera.transform.rotation;
        }
    }

    public void SetStyle(Color bgColor, Color textColor)
    {
        if (backgroundImage != null)
            backgroundImage.color = bgColor;
        if (speechText != null)
            speechText.color = textColor;
    }

    public void ShowSpeech(string message, float duration = 2.5f)
    {
        // 이전 애니메이션과 코루틴 취소
        CancelPreviousAnimation();
        
        if (speechText != null)
            speechText.text = message;
        
        gameObject.SetActive(true);
        
        // 알파값을 1로 리셋
        ResetAlpha();
        
        // 일정 시간 후 페이드아웃
        hideCoroutine = StartCoroutine(HideAfterSeconds(duration));
    }

    private void CancelPreviousAnimation()
    {
        // 이전 코루틴 취소
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        
        // 이전 DOTween 시퀀스 취소
        if (fadeSequence != null && fadeSequence.IsActive())
        {
            fadeSequence.Kill();
            fadeSequence = null;
        }
    }

    private void ResetAlpha()
    {
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
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        
        // 페이드아웃 효과
        float fadeDuration = 0.5f;
        fadeSequence = DOTween.Sequence();
        
        if (backgroundImage != null)
            fadeSequence.Join(backgroundImage.DOFade(0f, fadeDuration));
        if (speechText != null)
            fadeSequence.Join(speechText.DOFade(0f, fadeDuration));
        
        yield return fadeSequence.WaitForCompletion();
        
        Hide();
    }

    public void Hide()
    {
        CancelPreviousAnimation();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 애니메이션 정리
        CancelPreviousAnimation();
    }
}