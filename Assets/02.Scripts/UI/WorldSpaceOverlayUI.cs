using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Linq;

/// <summary>
/// VR 환경에서 World Space UI가 오브젝트를 투과하여 항상 최상단에 렌더링되도록 하는 스크립트
/// </summary>
public class WorldSpaceOverlayUI : MonoBehaviour
{
    [Header("Z-Test Settings")]
    [SerializeField] private CompareFunction desiredUIComparison = CompareFunction.Always;
    
    [Header("UI Elements")]
    [Tooltip("Set to blank to automatically populate from the child UI elements")]
    [SerializeField] private Graphic[] uiElementsToApplyTo;

    // 재사용 가능한 머티리얼 매핑
    private Dictionary<Material, Material> materialMappings = new Dictionary<Material, Material>();
    
    // Z-Test 모드를 설정하는 셰이더 프로퍼티
    private const string shaderTestMode = "unity_GUIZTestMode";

    protected virtual void Start()
    {
        // UI 요소가 할당되지 않았으면 자동으로 찾기
        if (uiElementsToApplyTo.Length == 0)
        {
            uiElementsToApplyTo = gameObject.GetComponentsInChildren<Graphic>();
        }

        // 각 UI 요소에 Z-Test 모드 적용
        ApplyZTestModeToUIElements();
    }

    private void ApplyZTestModeToUIElements()
    {
        foreach (var graphic in uiElementsToApplyTo)
        {
            if (graphic == null) continue;

            Material material = graphic.materialForRendering;
            if (material == null)
            {
                Debug.LogError($"{nameof(WorldSpaceOverlayUI)}: skipping target without material {graphic.name}.{graphic.GetType().Name}");
                continue;
            }

            // 머티리얼 복사본 생성 또는 재사용
            if (!materialMappings.TryGetValue(material, out Material materialCopy))
            {
                materialCopy = new Material(material);
                materialMappings.Add(material, materialCopy);
            }

            // Z-Test 모드 설정
            materialCopy.SetInt(shaderTestMode, (int)desiredUIComparison);
            graphic.material = materialCopy;
        }
    }

    /// <summary>
    /// 런타임에 UI 요소 추가
    /// </summary>
    public void AddUIElement(Graphic graphic)
    {
        if (graphic == null) return;

        // 이미 존재하는지 확인
        if (uiElementsToApplyTo.Contains(graphic))
        {
            return; // 이미 존재하면 추가하지 않음
        }

        // 기존 배열에 새 요소 추가
        var newArray = new Graphic[uiElementsToApplyTo.Length + 1];
        uiElementsToApplyTo.CopyTo(newArray, 0);
        newArray[uiElementsToApplyTo.Length] = graphic;
        uiElementsToApplyTo = newArray;

        // 새 요소에 Z-Test 모드 적용
        ApplyZTestModeToUIElements();
    }

    /// <summary>
    /// 런타임에 UI 요소 제거
    /// </summary>
    public void RemoveUIElement(Graphic graphic)
    {
        if (graphic == null) return;

        // 배열에서 해당 요소 제거
        uiElementsToApplyTo = uiElementsToApplyTo.Where(g => g != graphic).ToArray();

        // 머티리얼 정리는 수동으로 호출하거나 OnDestroy에서만 수행
        // CleanupUnusedMaterials(); // 주석 처리하여 다른 말풍선에 영향 방지
    }

    /// <summary>
    /// 안전하게 UI 요소 제거 (머티리얼 정리 포함)
    /// </summary>
    public void RemoveUIElementSafely(Graphic graphic)
    {
        if (graphic == null) return;

        // 해당 요소의 머티리얼만 정리
        CleanupSpecificMaterial(graphic);

        // 배열에서 해당 요소 제거
        uiElementsToApplyTo = uiElementsToApplyTo.Where(g => g != graphic).ToArray();
    }

    /// <summary>
    /// 특정 UI 요소의 머티리얼만 정리
    /// </summary>
    private void CleanupSpecificMaterial(Graphic graphic)
    {
        if (graphic == null || graphic.materialForRendering == null) return;

        Material originalMaterial = graphic.materialForRendering;
        
        // 해당 머티리얼의 복사본이 있는지 확인
        if (materialMappings.TryGetValue(originalMaterial, out Material materialCopy))
        {
            // 해당 머티리얼을 사용하는 다른 UI 요소가 있는지 확인
            bool isUsedByOthers = uiElementsToApplyTo.Any(g => 
                g != graphic && g != null && g.materialForRendering == originalMaterial);

            // 다른 요소가 사용하지 않으면 정리
            if (!isUsedByOthers)
            {
                if (materialCopy != null)
                {
                    DestroyImmediate(materialCopy);
                }
                materialMappings.Remove(originalMaterial);
            }
        }
    }

    /// <summary>
    /// 사용되지 않는 머티리얼 정리
    /// </summary>
    private void CleanupUnusedMaterials()
    {
        var usedMaterials = new HashSet<Material>();
        
        // 현재 사용 중인 머티리얼 수집
        foreach (var graphic in uiElementsToApplyTo)
        {
            if (graphic != null && graphic.materialForRendering != null)
            {
                usedMaterials.Add(graphic.materialForRendering);
            }
        }

        // 사용되지 않는 머티리얼 복사본 제거
        var materialsToRemove = new List<Material>();
        foreach (var kvp in materialMappings)
        {
            if (!usedMaterials.Contains(kvp.Key))
            {
                materialsToRemove.Add(kvp.Value);
            }
        }

        foreach (var materialCopy in materialsToRemove)
        {
            if (materialCopy != null)
            {
                DestroyImmediate(materialCopy);
            }
            materialMappings.Remove(materialMappings.FirstOrDefault(x => x.Value == materialCopy).Key);
        }
    }

    /// <summary>
    /// 모든 UI 요소를 다시 스캔하고 적용
    /// </summary>
    [ContextMenu("Refresh UI Elements")]
    public void RefreshUIElements()
    {
        // 기존 머티리얼 매핑 정리
        foreach (var materialCopy in materialMappings.Values)
        {
            if (materialCopy != null)
            {
                DestroyImmediate(materialCopy);
            }
        }
        materialMappings.Clear();

        // UI 요소 다시 찾기
        uiElementsToApplyTo = gameObject.GetComponentsInChildren<Graphic>();
        
        // Z-Test 모드 다시 적용
        ApplyZTestModeToUIElements();
    }

    private void OnDestroy()
    {
        // 생성된 머티리얼 복사본들 정리
        foreach (var materialCopy in materialMappings.Values)
        {
            if (materialCopy != null)
            {
                DestroyImmediate(materialCopy);
            }
        }
        materialMappings.Clear();
    }

    /// <summary>
    /// Z-Test 비교 함수 설명
    /// </summary>
    [System.Serializable]
    public enum ZTestMode
    {
        [Tooltip("항상 렌더링 (오브젝트 투과)")]
        Always = (int)CompareFunction.Always,
        
        [Tooltip("같거나 앞에 있을 때 렌더링")]
        LEqual = (int)CompareFunction.LessEqual,
        
        [Tooltip("앞에 있을 때만 렌더링")]
        Less = (int)CompareFunction.Less,
        
        [Tooltip("같을 때만 렌더링")]
        Equal = (int)CompareFunction.Equal,
        
        [Tooltip("같지 않을 때 렌더링")]
        NotEqual = (int)CompareFunction.NotEqual,
        
        [Tooltip("같거나 뒤에 있을 때 렌더링")]
        GEqual = (int)CompareFunction.GreaterEqual,
        
        [Tooltip("뒤에 있을 때만 렌더링")]
        Greater = (int)CompareFunction.Greater,
        
        [Tooltip("절대 렌더링하지 않음")]
        Never = (int)CompareFunction.Never
    }
} 