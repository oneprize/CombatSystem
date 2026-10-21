using UnityEngine;

/// <summary>
/// 카메라 리그의 기준점을 플레이어 위치(+오프셋)에 붙여 두는 컴포넌트.
/// 회전은 따라가지 않으므로 카메라 회전은 CameraRig 입력만으로 결정된다.
/// </summary>
public class CameraRigAnchor : MonoBehaviour
{
    public Transform player;
    public Vector3 offset = new Vector3(0f, 1.6f, 0f);

    /// <summary>플레이어 이동이 끝난 뒤 앵커 위치를 플레이어 위치 + 오프셋으로 맞춘다.</summary>
    void LateUpdate()
    {
        if (!player) return;
        transform.position = player.position + offset; // 회전은 무시하고 위치만 따라간다
    }
}
