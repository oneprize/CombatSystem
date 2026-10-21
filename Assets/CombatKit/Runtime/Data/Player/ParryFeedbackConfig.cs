using UnityEngine;

namespace CombatSystem.Data.Player
{
    /// <summary>패리·저스트 회피 성공 시 재생되는 슬로 모션과 카메라 줌 연출의 강도/타이밍 설정.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Player/Parry Feedback Config", fileName = "ParryFeedbackConfig")]
    public class ParryFeedbackConfig : ScriptableObject
    {
        /// <summary>연출 중 원래 timeScale에 곱하는 배율(작을수록 느려짐).</summary>
        [Range(0.05f, 1f)] public float slowMotionScale = 0.1f;
        /// <summary>줌인에 걸리는 실제 시간(초, unscaled).</summary>
        [Min(0.01f)] public float zoomInDuration = 0.08f;
        /// <summary>줌인 후 슬로 모션과 줌을 유지하는 실제 시간(초).</summary>
        [Min(0f)] public float holdDuration = 0.22f;
        /// <summary>유지 후 원래 시간 배율과 줌으로 되돌아가는 실제 시간(초).</summary>
        [Min(0.01f)] public float recoveryDuration = 0.35f;
        /// <summary>줌 최대치에서 원래 FOV(또는 orthographic size)에 곱하는 배율. 1보다 작으면 줌인, 크면 줌아웃.</summary>
        [Range(0.3f, 2f)] public float zoomMultiplier = 0.7f;
    }
}
