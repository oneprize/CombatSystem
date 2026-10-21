using UnityEngine;
namespace CombatSystem.Data.Boss
{
    /// <summary>보스 착지 애니메이션의 발 접지 보정 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Landing Grounding Config", fileName = "BossLandingGroundingConfig")]
    public class BossLandingGroundingConfig : ScriptableObject
    {
        /// <summary>발바닥이 바닥보다 최소한 떠 있어야 하는 높이. 보스 스케일에 비례해 적용된다.</summary>
        [Min(0)] public float soleClearance = .02f;
    }
}
