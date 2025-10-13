using UnityEngine;

/// <summary>
/// 단순 추적 카메라. 인스펙터에서 target을 연결하면 해당 Transform을 따라갑니다.
/// 오프셋과 회전 고정 옵션을 제공합니다.
/// </summary>
public class ActorFollowCam : MonoBehaviour
{
    [SerializeField]
    private Transform target;
    private Vector3 focusTarget;
    private Transform focusTargetTransform;
    [Header("Focus Settings")]
    [SerializeField] private float focusSpeed = 5f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 3f;
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 100f;

    private Camera cam;
    private float targetZoom;

    void Awake()
    {
        if (target != null)
        {
            focusTargetTransform = target;
            // 초기 위치 계산
            Vector3 initialFocusTarget = CalculateFocusPosition(target.position);
            focusTarget = new Vector3(initialFocusTarget.x, transform.position.y, initialFocusTarget.z);
        }

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            targetZoom = cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
        }
    }

    private void LateUpdate()
    {

        if (focusTargetTransform != null)
        {
            var newFocus = CalculateFocusPosition(focusTargetTransform.position);
            focusTarget = new Vector3(newFocus.x, transform.position.y, newFocus.z);
        }
        transform.position = Vector3.Lerp(transform.position, focusTarget, Time.deltaTime * focusSpeed);

        // 마우스 휠 줌 처리 및 적용 (CameraController와 동일한 개념)
        HandleZoom();
        ApplyZoom();
    }

    /// <summary>
    /// 카메라 회전을 고려하여 캐릭터가 화면 중앙에 오도록 카메라 위치를 계산합니다
    /// (CameraController.CalculateFocusPosition과 동일)
    /// </summary>
    private Vector3 CalculateFocusPosition(Vector3 characterPosition)
    {
        float focusDistance = 3f;
        Vector3 rotationOffset = new Vector3(0f, 0f, -24.6638f);

        float cameraYRotation = this.transform.eulerAngles.y * Mathf.Deg2Rad;
        float verticalDistance = focusDistance * Mathf.Sin(65f * Mathf.Deg2Rad);
        float horizontalDistance = focusDistance * Mathf.Cos(65f * Mathf.Deg2Rad);

        float offsetX = horizontalDistance * Mathf.Sin(cameraYRotation);
        float offsetZ = horizontalDistance * Mathf.Cos(cameraYRotation);

        Vector3 calculatedOffset = new Vector3(offsetX, 0f, offsetZ);

        Vector3 cameraForward = this.transform.forward;
        Vector3 baseOffset = -cameraForward * focusDistance;

        Vector3 targetPosition = characterPosition + baseOffset + rotationOffset;
        targetPosition.y = this.transform.position.y;

        return targetPosition;
    }

    private void HandleZoom()
    {
        if (cam == null) return;
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            if (cam.orthographic)
            {
                targetZoom -= scrollInput * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
            else
            {
                targetZoom -= scrollInput * zoomSpeed * 10f;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }
    }

    private void ApplyZoom()
    {
        if (cam == null) return;
        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 10f);
        }
        else
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetZoom, Time.deltaTime * 10f);
        }
    }
}


