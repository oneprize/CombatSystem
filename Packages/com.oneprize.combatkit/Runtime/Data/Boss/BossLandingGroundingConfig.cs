using UnityEngine;
namespace CombatSystem.Data.Boss
{
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Landing Grounding Config", fileName = "BossLandingGroundingConfig")]
    public class BossLandingGroundingConfig : ScriptableObject
    {
        [Min(0)] public float soleClearance = .02f;
    }
}
