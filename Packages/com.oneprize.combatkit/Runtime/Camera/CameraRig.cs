using UnityEngine;
using UnityEngine.InputSystem;

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
    public bool lockCursor = true;

    float yaw;
    float pitch;

    void Start()
    {
        Vector3 e = transform.localEulerAngles;
        yaw = e.y; pitch = e.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        // x0.1 matches the legacy "Mouse X/Y" axis sensitivity this rig was tuned with.
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
