using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 빌딩 내부 시뮬레이션의 나레이션을 표시하는 UI
/// </summary>
public class NarrativeUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject narrativePanel;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject narrativeEntryPrefab;
    [SerializeField] private ScrollRect scrollRect;
    
    [Header("Settings")]
    [SerializeField] private int maxEntries = 20;
    [SerializeField] private float autoScrollDuration = 0.5f;
    
    private List<GameObject> narrativeEntries = new List<GameObject>();
    private bool isInitialized = false;

    private void Awake()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (isInitialized) return;
        
        // 패널이 없으면 생성
        if (narrativePanel == null)
        {
            CreateNarrativePanel();
        }
        
        isInitialized = true;
    }

    private void CreateNarrativePanel()
    {
        // Canvas 찾기
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasObj = new GameObject("NarrativeCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 패널 생성
        narrativePanel = new GameObject("NarrativePanel");
        narrativePanel.transform.SetParent(canvas.transform, false);
        
        var panelImage = narrativePanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        
        var panelRect = narrativePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0.4f, 0.3f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // ScrollRect 생성
        var scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(narrativePanel.transform, false);
        
        var scrollRectComponent = scrollView.AddComponent<ScrollRect>();
        scrollRect = scrollRectComponent;
        
        var scrollRectTransform = scrollView.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(10, 10);
        scrollRectTransform.offsetMax = new Vector2(-10, -10);

        // Content 생성
        var content = new GameObject("Content");
        content.transform.SetParent(scrollView.transform, false);
        
        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 5;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.childControlHeight = false;
        contentLayout.childControlWidth = true;
        
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        scrollRectComponent.content = contentRect;
        contentParent = content.transform;

        // Viewport 생성
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        
        var viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0.5f);
        
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        var viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // Content를 Viewport의 자식으로 이동
        content.transform.SetParent(viewport.transform, false);
        scrollRectComponent.viewport = viewportRect;

        // 기본 엔트리 프리팹 생성
        CreateDefaultEntryPrefab();
    }

    private void CreateDefaultEntryPrefab()
    {
        narrativeEntryPrefab = new GameObject("NarrativeEntryPrefab");
        narrativeEntryPrefab.SetActive(false);
        
        var entryImage = narrativeEntryPrefab.AddComponent<Image>();
        entryImage.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        
        var entryLayout = narrativeEntryPrefab.AddComponent<LayoutElement>();
        entryLayout.minHeight = 30;
        entryLayout.preferredHeight = 30;
        
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(narrativeEntryPrefab.transform, false);
        
        var textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = "Sample Text";
        textComponent.color = Color.white;
        textComponent.fontSize = 12;
        textComponent.alignment = TextAlignmentOptions.Left;
        
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);
    }

    /// <summary>
    /// 나레이션 엔트리 추가
    /// </summary>
    public void AddNarrativeEntry(string narrativeText)
    {
        if (!isInitialized)
        {
            InitializeUI();
        }

        // 새 엔트리 생성
        GameObject entry = Instantiate(narrativeEntryPrefab, contentParent);
        entry.SetActive(true);
        
        var textComponent = entry.GetComponentInChildren<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = narrativeText;
        }

        // 페이드인 애니메이션
        var canvasGroup = entry.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = entry.AddComponent<CanvasGroup>();
        }
        
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);

        narrativeEntries.Add(entry);

        // 최대 개수 제한
        if (narrativeEntries.Count > maxEntries)
        {
            RemoveOldestEntry();
        }

        // 자동 스크롤
        AutoScrollToBottom();
    }

    /// <summary>
    /// 가장 오래된 엔트리 제거
    /// </summary>
    private void RemoveOldestEntry()
    {
        if (narrativeEntries.Count > 0)
        {
            var oldestEntry = narrativeEntries[0];
            narrativeEntries.RemoveAt(0);
            
            // 페이드아웃 애니메이션
            var canvasGroup = oldestEntry.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, 0.3f).OnComplete(() => {
                    Destroy(oldestEntry);
                });
            }
            else
            {
                Destroy(oldestEntry);
            }
        }
    }

    /// <summary>
    /// 하단으로 자동 스크롤
    /// </summary>
    private void AutoScrollToBottom()
    {
        if (scrollRect != null)
        {
            DOVirtual.DelayedCall(0.1f, () => {
                scrollRect.verticalNormalizedPosition = 0f;
            });
        }
    }

    /// <summary>
    /// 모든 나레이션 엔트리 제거
    /// </summary>
    public void ClearAllEntries()
    {
        foreach (var entry in narrativeEntries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }
        narrativeEntries.Clear();
    }

    /// <summary>
    /// 패널 표시/숨김 토글
    /// </summary>
    public void TogglePanel()
    {
        if (narrativePanel != null)
        {
            narrativePanel.SetActive(!narrativePanel.activeSelf);
        }
    }

    /// <summary>
    /// 패널 표시
    /// </summary>
    public void ShowPanel()
    {
        if (narrativePanel != null)
        {
            narrativePanel.SetActive(true);
        }
    }

    /// <summary>
    /// 패널 숨김
    /// </summary>
    public void HidePanel()
    {
        if (narrativePanel != null)
        {
            narrativePanel.SetActive(false);
        }
    }
} 