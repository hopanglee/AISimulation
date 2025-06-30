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
            // 포커스 중일 때는 목표 위치로 부드럽게 이동
            transform.position = Vector3.Lerp(transform.position, focusTarget, Time.deltaTime * focusSpeed);
            
            // 목표 위치에 충분히 가까워지면 포커스 모드 해제
            if (Vector3.Distance(transform.position, focusTarget) < 0.1f)
            {
                isFocusing = false;
                targetPosition = transform.position;
            }
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
    }
    
    /// <summary>
    /// 포커스 모드를 해제하고 일반 이동 모드로 돌아갑니다
    /// </summary>
    public void StopFocus()
    {
        isFocusing = false;
        targetPosition = transform.position;
    }
}
