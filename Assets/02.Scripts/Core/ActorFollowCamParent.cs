using UnityEngine;

/// <summary>
/// 대상의 X/Z 위치만 따라가는 간단한 추적 컴포넌트.
/// 인스펙터에서 target을 지정하세요.
/// </summary>
public class ActorFollowCamParent : MonoBehaviour
{
    [SerializeField]
    private Transform target;
    [Header("Focus Settings")]
    [SerializeField] private float focusSpeed = 5f;
    [Header("Orbit Settings")]
    [SerializeField] private float orbitSensitivityX = 1f; // 좌우 드래그 민감도 (Y축 회전)
    [SerializeField] private float orbitSensitivityY = 1f; // 상하 드래그 민감도 (X축 회전)

    private float yaw;   // Y축 회전 (수평)
    private float pitch; // X축 회전 (수직)

    private void Awake()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        pitch = euler.x;
        yaw = euler.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 current = transform.position;
        Vector3 t = target.position;
        var focusTarget = new Vector3(t.x, current.y, t.z);
        transform.position = Vector3.Lerp(transform.position, focusTarget, Time.deltaTime * focusSpeed);

        HandleOrbit();
    }

    /// <summary>
    /// 마우스 드래그 양에 따라 현재 기준에서 회전합니다.
    /// - 가로(우측 드래그): Y 회전 감소, 가로(좌측 드래그): Y 회전 증가
    /// - 세로(위 드래그): X 회전 감소, 세로(아래 드래그): X 회전 증가
    /// </summary>
    private void HandleOrbit()
    {
        // 우클릭이 아닐 때는 동작하지 않음
        if (!Input.GetMouseButton(1)) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        if (mouseX == 0f && mouseY == 0f) return;

        // 우로 드래그할수록 Y 감소, 좌로 드래그할수록 Y 증가
        yaw -= mouseX * orbitSensitivityX;
        // 위로 드래그할수록 X 감소, 아래로 드래그할수록 X 증가
        pitch -= mouseY * orbitSensitivityY;

        transform.eulerAngles = new Vector3(pitch, yaw, 0f);
    }
}


