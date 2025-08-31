using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 20f;
    
    [Header("Movement Bounds")]
    [SerializeField] private float minX = -50f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minZ = -50f;
    [SerializeField] private float maxZ = 50f;
    
    [Header("Focus Settings")]
    [SerializeField] private float focusSpeed = 5f;
    
    private Camera mainCamera;
    private Vector3 targetPosition;
    private float targetZoom;
    private bool isFocusing = false;
    private Vector3 focusTarget;
    private Transform focusTargetTransform; // 포커스할 대상의 Transform
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        // 초기 위치와 줌 설정
        targetPosition = transform.position;
        targetZoom = mainCamera.orthographic ? mainCamera.orthographicSize : mainCamera.fieldOfView;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isFocusing)
        {
            HandleMovement();
            HandleZoom();
        }
        else
        {
            // 포커스 중일 때도 줌은 가능하도록
            HandleZoom();
            
            // 방향키 입력 감지하여 포커스 해제
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ||
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ||
                Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                StopFocus();
            }
        }
        ApplyMovement();
        ApplyZoom();
    }
    
    private void HandleMovement()
    {
        Vector3 movement = Vector3.zero;
        
        // WASD 키 입력
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            movement += Vector3.forward;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            movement += Vector3.back;
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            movement += Vector3.left;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            movement += Vector3.right;
        }
        
        // 이동 속도 적용
        movement = movement.normalized * moveSpeed * Time.deltaTime;
        
        // Y축은 고정하고 XZ 평면에서만 이동
        movement.y = 0;
        
        // 목표 위치 업데이트
        targetPosition += movement;
        
        // 경계 제한
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);
    }
    
    private void HandleZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        
        if (scrollInput != 0)
        {
            if (mainCamera.orthographic)
            {
                // 직교 카메라의 경우 orthographicSize 조정
                targetZoom -= scrollInput * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
            else
            {
                // 원근 카메라의 경우 fieldOfView 조정
                targetZoom -= scrollInput * zoomSpeed * 10f;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }
    }
    
    private void ApplyMovement()
    {
        if (isFocusing)
        {
            // 포커스 중일 때는 대상이 계속 움직이므로 실시간으로 위치 업데이트
            if (focusTargetTransform != null)
            {
                // 대상의 현재 위치를 기반으로 카메라 위치 계산
                Vector3 newFocusTarget = CalculateFocusPosition(focusTargetTransform.position);
                focusTarget = new Vector3(newFocusTarget.x, transform.position.y, newFocusTarget.z);
            }
            
            // 목표 위치로 부드럽게 이동
            transform.position = Vector3.Lerp(transform.position, focusTarget, Time.deltaTime * focusSpeed);
        }
        else
        {
            // 일반 이동 모드
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        }
    }
    
    private void ApplyZoom()
    {
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetZoom, Time.deltaTime * 10f);
        }
        else
        {
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetZoom, Time.deltaTime * 10f);
        }
    }
    
    /// <summary>
    /// 특정 타겟 위치로 카메라를 포커스합니다 (Y축은 유지)
    /// </summary>
    /// <param name="targetPosition">포커스할 위치</param>
    public void FocusOnTarget(Vector3 targetPosition)
    {
        // Y축은 현재 카메라 높이 유지
        focusTarget = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
        isFocusing = true;
        focusTargetTransform = null; // 위치 기반 포커스
    }
    
    /// <summary>
    /// 특정 Transform을 계속 따라다니도록 포커스합니다
    /// </summary>
    /// <param name="targetTransform">포커스할 Transform</param>
    public void FocusOnTransform(Transform targetTransform)
    {
        if (targetTransform != null)
        {
            focusTargetTransform = targetTransform;
            // 초기 위치 계산
            Vector3 initialFocusTarget = CalculateFocusPosition(targetTransform.position);
            focusTarget = new Vector3(initialFocusTarget.x, transform.position.y, initialFocusTarget.z);
            isFocusing = true;
        }
    }
    
    /// <summary>
    /// 포커스 모드를 해제하고 일반 이동 모드로 돌아갑니다
    /// </summary>
    public void StopFocus()
    {
        isFocusing = false;
        focusTargetTransform = null;
        targetPosition = transform.position;
    }
    
    /// <summary>
    /// 캐릭터 위치를 기반으로 카메라 위치를 계산합니다
    /// </summary>
    /// <param name="characterPosition">캐릭터의 위치</param>
    /// <returns>카메라가 위치해야 할 위치</returns>
    // private Vector3 CalculateFocusPosition(Vector3 characterPosition)
    // {
    //     // 카메라의 현재 방향을 고려하여 캐릭터 뒤쪽에 위치하도록 계산
    //     Vector3 cameraForward = transform.forward;
    //     Vector3 offset = -cameraForward * 3f; // 기본 거리
        
    //     // Y축은 현재 카메라 높이 유지
    //     Vector3 targetPos = characterPosition + offset;
    //     targetPos.y = transform.position.y;
        
    //     return targetPos;
    // }

/// <summary>
    /// 카메라 회전을 고려하여 캐릭터가 화면 중앙에 오도록 카메라 위치를 계산합니다
    /// </summary>
    /// <param name="characterPosition">캐릭터의 위치</param>
    /// <returns>카메라가 이동해야 할 위치</returns>
    private Vector3 CalculateFocusPosition(Vector3 characterPosition)
    {
        float focusDistance = 3f;
        // 방법 1: 사용자가 제공한 오프셋 값 사용
        Vector3 rotationOffset = new Vector3(0f, 0f, -24.6638f);
        
        // 방법 2: 삼각함수를 이용한 수학적 계산
        // 카메라의 Y축 회전을 라디안으로 변환
        float cameraYRotation = this.transform.eulerAngles.y * Mathf.Deg2Rad;
        
        // 65도 기울어진 카메라에서의 수직 거리 계산
        float verticalDistance = focusDistance * Mathf.Sin(65f * Mathf.Deg2Rad);
        float horizontalDistance = focusDistance * Mathf.Cos(65f * Mathf.Deg2Rad);
        
        // 카메라 회전에 따른 X, Z 오프셋 계산
        float offsetX = horizontalDistance * Mathf.Sin(cameraYRotation);
        float offsetZ = horizontalDistance * Mathf.Cos(cameraYRotation);
        
        Vector3 calculatedOffset = new Vector3(offsetX, 0f, offsetZ);
        
        // 캐릭터 위치에서 기본 오프셋 적용
        Vector3 cameraForward = this.transform.forward;
        Vector3 baseOffset = -cameraForward * focusDistance;
        
        // 계산된 오프셋과 사용자 제공 오프셋 중 선택 (현재는 사용자 제공 값 사용)
        Vector3 targetPosition = characterPosition + baseOffset + rotationOffset;
        
        // Y축은 현재 카메라 높이 유지
        targetPosition.y = this.transform.position.y;
        
        return targetPosition;
    }
}
