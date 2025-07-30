using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Pool;

public class SpeechBubbleUI : MonoBehaviour
{
    [Header("Speech Bubble Prefab")]
    [SerializeField] private GameObject speechBubbleItemPrefab;
    
    [Header("Layout Settings")]
    [SerializeField] private VerticalLayoutGroup verticalLayoutGroup;
    [SerializeField] private ContentSizeFitter contentSizeFitter;
    
    [Header("Pool Settings")]
    [SerializeField] private int defaultCapacity = 10;
    [SerializeField] private int maxSize = 20;
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float defaultDuration = 3f;
    [SerializeField] private float spacingBetweenBubbles = 0.2f;

    [Header("Speech Bubble Style")]
    [SerializeField] private Color defaultBgColor = new Color(1f, 1f, 1f, 0.9f); // 흰색 배경
    [SerializeField] private Color defaultTextColor = Color.black; // 검정 텍스트

    [Header("UI Overlay Support")]
    [SerializeField] private WorldSpaceOverlayUI worldSpaceOverlayUI;

    private ObjectPool<SpeechBubbleItem> speechBubblePool;
    private List<SpeechBubbleItem> activeBubbles = new List<SpeechBubbleItem>();
    private Camera mainCamera;
    [SerializeField] private Transform canvas;
    
    // 연타 방지를 위한 플래그
    private bool isProcessingMultipleSpeech = false;

    [System.Serializable]
    public class SpeechBubbleItem : MonoBehaviour
    {
        public TMP_Text textComponent;
        public Image backgroundImage;
        public RectTransform rectTransform;
        public Coroutine fadeCoroutine;
        public Sequence fadeSequence;
        
        public void Initialize(string message, Color bgColor, Color textColor)
        {
            if (textComponent != null)
                textComponent.text = message;
            
            if (backgroundImage != null)
            {
                // 현재 알파값을 유지하면서 색상 설정
                var currentBgColor = backgroundImage.color;
                bgColor.a = currentBgColor.a;
                backgroundImage.color = bgColor;
            }
            
            if (textComponent != null)
            {
                // 현재 알파값을 유지하면서 색상 설정
                var currentTextColor = textComponent.color;
                textColor.a = currentTextColor.a;
                textComponent.color = textColor;
            }
        }
        
        public void SetAlpha(float alpha)
        {
            if (backgroundImage != null)
            {
                var bgColor = backgroundImage.color;
                bgColor.a = alpha;
                backgroundImage.color = bgColor;
            }
            
            if (textComponent != null)
            {
                var textColor = textComponent.color;
                textColor.a = alpha;
                textComponent.color = textColor;
            }
        }
        
        public void FadeIn(float duration)
        {
            if (fadeSequence != null && fadeSequence.IsActive())
                fadeSequence.Kill();
                
            fadeSequence = DOTween.Sequence();
            fadeSequence.Append(DOTween.To(() => 0f, SetAlpha, 1f, duration));
        }
        
        public void FadeOut(float duration, System.Action onComplete = null)
        {
            if (fadeSequence != null && fadeSequence.IsActive())
                fadeSequence.Kill();
                
            fadeSequence = DOTween.Sequence();
            
            // Background와 Text를 동시에 페이드아웃
            fadeSequence.Append(DOTween.To(() => 1f, SetAlpha, 0f, duration));
            
            // 완료 후 콜백 실행
            fadeSequence.OnComplete(() => 
            {
                // 확실히 알파값을 0으로 설정
                SetAlpha(0f);
                onComplete?.Invoke();
            });
        }
    }

    private void Awake()
    {
        // 메인 카메라 찾기
        mainCamera = Camera.main;
        
        // Pool 초기화
        InitializePool();
        
        // WorldSpaceOverlayUI 자동 찾기
        if (worldSpaceOverlayUI == null)
        {
            worldSpaceOverlayUI = GetComponent<WorldSpaceOverlayUI>();
        }
        
        // 초기 상태는 숨김
        gameObject.SetActive(false);
    }

    private void InitializePool()
    {
        speechBubblePool = new ObjectPool<SpeechBubbleItem>(
            createFunc: () =>
            {
                GameObject obj = Instantiate(speechBubbleItemPrefab, verticalLayoutGroup.transform);
                SpeechBubbleItem item = obj.GetComponent<SpeechBubbleItem>();
                if (item == null)
                {
                    item = obj.AddComponent<SpeechBubbleItem>();
                    item.textComponent = obj.GetComponentInChildren<TMP_Text>();
                    item.backgroundImage = obj.GetComponentInChildren<Image>();
                    item.rectTransform = obj.GetComponent<RectTransform>();
                }
                
                // 생성 시 알파값을 0으로 초기화
                item.SetAlpha(0f);
                return item;
            },
            actionOnGet: (item) =>
            {
                item.gameObject.SetActive(true);
                
                // WorldSpaceOverlayUI에 새로 생성된 UI 요소 추가
                if (worldSpaceOverlayUI != null)
                {
                    // Text와 Image 컴포넌트를 모두 추가
                    if (item.textComponent != null)
                    {
                        worldSpaceOverlayUI.AddUIElement(item.textComponent);
                    }
                    if (item.backgroundImage != null)
                    {
                        worldSpaceOverlayUI.AddUIElement(item.backgroundImage);
                    }
                }
            },
            actionOnRelease: (item) =>
            {
                if (item.fadeCoroutine != null)
                {
                    StopCoroutine(item.fadeCoroutine);
                    item.fadeCoroutine = null;
                }
                if (item.fadeSequence != null && item.fadeSequence.IsActive())
                {
                    item.fadeSequence.Kill();
                    item.fadeSequence = null;
                }
                
                // Pool로 반환할 때 확실히 알파값을 0으로 설정
                item.SetAlpha(0f);
                
                // WorldSpaceOverlayUI에서 UI 요소 안전하게 제거
                if (worldSpaceOverlayUI != null)
                {
                    if (item.textComponent != null)
                    {
                        worldSpaceOverlayUI.RemoveUIElementSafely(item.textComponent);
                    }
                    if (item.backgroundImage != null)
                    {
                        worldSpaceOverlayUI.RemoveUIElementSafely(item.backgroundImage);
                    }
                }
                
                item.gameObject.SetActive(false);
            },
            actionOnDestroy: (item) =>
            {
                if (item != null && item.gameObject != null)
                    Destroy(item.gameObject);
            },
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    private void LateUpdate()
    {
        // Canvas 회전을 카메라 방향으로 고정 (Actor 회전에 영향받지 않도록)
        if (mainCamera != null && canvas != null && gameObject.activeInHierarchy)
        {
            // Canvas를 카메라 방향으로 회전
            canvas.rotation = mainCamera.transform.rotation;
        }
    }

    public void ShowSpeech(string message, float duration = -1f, Color? bgColor = null, Color? textColor = null)
    {
        if (duration < 0) duration = defaultDuration;
        
        // 색상 설정 (기본값 또는 커스텀 색상)
        Color finalBgColor = bgColor ?? defaultBgColor;
        Color finalTextColor = textColor ?? defaultTextColor;
        
        // 말풍선 아이템 생성
        SpeechBubbleItem bubbleItem = speechBubblePool.Get();
        activeBubbles.Add(bubbleItem);
        
        // VerticalLayoutGroup의 맨 아래에 추가 (순서 보장)
        bubbleItem.rectTransform.SetAsLastSibling();
        
        // 새로 생성된 말풍선의 알파값을 0으로 설정 (다른 말풍선에 영향 없음)
        bubbleItem.SetAlpha(0f);
        
        // 말풍선 초기화
        bubbleItem.Initialize(message, finalBgColor, finalTextColor);
        
        // 페이드인 효과
        bubbleItem.FadeIn(fadeInDuration);
        
        // 컨테이너 활성화
        gameObject.SetActive(true);
        
        // 일정 시간 후 제거
        bubbleItem.fadeCoroutine = StartCoroutine(RemoveBubbleAfterDelay(bubbleItem, duration));
    }

    private IEnumerator RemoveBubbleAfterDelay(SpeechBubbleItem bubbleItem, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 페이드아웃 후 제거
        bubbleItem.FadeOut(fadeOutDuration, () =>
        {
            activeBubbles.Remove(bubbleItem);
            speechBubblePool.Release(bubbleItem);
            
            // 활성화된 말풍선이 없으면 컨테이너 숨김
            if (activeBubbles.Count == 0)
            {
                gameObject.SetActive(false);
            }
        });
    }

    public void ClearAllSpeech()
    {
        // 모든 활성화된 말풍선 제거
        foreach (var bubble in activeBubbles.ToArray())
        {
            // 모든 애니메이션 즉시 중단
            if (bubble.fadeCoroutine != null)
            {
                StopCoroutine(bubble.fadeCoroutine);
                bubble.fadeCoroutine = null;
            }
            if (bubble.fadeSequence != null && bubble.fadeSequence.IsActive())
            {
                bubble.fadeSequence.Kill();
                bubble.fadeSequence = null;
            }
            
            // 즉시 투명하게 만들고 풀로 반환
            bubble.SetAlpha(0f);
            speechBubblePool.Release(bubble);
        }
        
        // WorldSpaceOverlayUI 정리 (모든 말풍선이 제거된 후에만)
        if (worldSpaceOverlayUI != null)
        {
            worldSpaceOverlayUI.RefreshUIElements();
        }
        
        activeBubbles.Clear();
        gameObject.SetActive(false);
    }

    public void ShowMultipleSpeech(List<string> messages, float durationPerMessage = -1f, Color? bgColor = null, Color? textColor = null)
    {
        if (durationPerMessage < 0) durationPerMessage = defaultDuration;
        
        // 중복 실행 방지
        if (isProcessingMultipleSpeech)
        {
            Debug.LogWarning("ShowMultipleSpeech is already processing. Ignoring new request.");
            return;
        }
        
        // 기존 말풍선 정리
        ClearAllSpeech();
        
        // GameObject가 비활성화되어 있으면 먼저 활성화
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        // 연타 방지 플래그 설정
        isProcessingMultipleSpeech = true;
        
        for (int i = 0; i < messages.Count; i++)
        {
            float delay = i * spacingBetweenBubbles;
            StartCoroutine(ShowSpeechWithDelay(messages[i], durationPerMessage, delay, bgColor, textColor));
        }
        
        // 모든 말풍선 생성 완료 후 플래그 해제
        StartCoroutine(ResetProcessingFlag(messages.Count * spacingBetweenBubbles + 0.1f));
    }

    private IEnumerator ShowSpeechWithDelay(string message, float duration, float delay, Color? bgColor = null, Color? textColor = null)
    {
        yield return new WaitForSeconds(delay);
        ShowSpeech(message, duration, bgColor, textColor);
    }

    private IEnumerator ResetProcessingFlag(float delay)
    {
        yield return new WaitForSeconds(delay);
        isProcessingMultipleSpeech = false;
    }

    private void OnDestroy()
    {
        // Pool 정리
        if (speechBubblePool != null)
        {
            speechBubblePool.Dispose();
        }
        
        // 활성화된 말풍선 정리
        foreach (var bubble in activeBubbles)
        {
            if (bubble != null && bubble.fadeCoroutine != null)
            {
                StopCoroutine(bubble.fadeCoroutine);
            }
        }
    }
}