using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스(또는 지정한 Look 액션) 입력으로 카메라 리그의 yaw/pitch를 회전시키는 단순 궤도 카메라 회전기.
/// pitch는 최소/최대 각도로 제한한다.
/// </summary>
public class CameraRig : MonoBehaviour
{
    [Header("Mouse Settings")]
    public float sensitivityX = 250f;
    public float sensitivityY = 200f;
    public float minPitch = -45f;
    public float maxPitch = 70f;

    [Header("Optional")]
    [Tooltip("Look action (Vector2 delta). Empty = read the mouse directly.")]
    public InputActionReference look;


    float yaw;
    float pitch;

    /// <summary>현재 로컬 회전을 시작 yaw/pitch로 삼아 첫 프레임에 시점이 튀지 않게 한다.</summary>
    void Start()
    {
        Vector3 e = transform.localEulerAngles;
        yaw = e.y; pitch = e.x;
    }

    /// <summary>입력 델타를 감도에 맞춰 yaw/pitch에 누적하고 pitch를 제한한 뒤 리그 회전에 반영한다.</summary>
    void LateUpdate()
    {
        // 0.1배는 이 리그가 원래 튜닝되었던 구 Input Manager "Mouse X/Y" 축 감도와 맞추기 위한 값이다.
        Vector2 delta = look != null && look.action != null ? look.action.ReadValue<Vector2>()
            : Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        float mx = delta.x * 0.1f;
        float my = delta.y * 0.1f;

        yaw += mx * sensitivityX * Time.deltaTime;
        pitch -= my * sensitivityY * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
