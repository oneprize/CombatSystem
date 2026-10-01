using UnityEngine;

namespace CombatSystem.Data.Player
{
    [CreateAssetMenu(menuName = "CombatKit/Player/Parry Feedback Config", fileName = "ParryFeedbackConfig")]
    public class ParryFeedbackConfig : ScriptableObject
    {
        [Range(0.05f, 1f)] public float slowMotionScale = 0.1f;
        [Min(0.01f)] public float zoomInDuration = 0.08f;
        [Min(0f)] public float holdDuration = 0.22f;
        [Min(0.01f)] public float recoveryDuration = 0.35f;
        [Range(0.3f, 1f)] public float zoomMultiplier = 0.7f;
    }
}
