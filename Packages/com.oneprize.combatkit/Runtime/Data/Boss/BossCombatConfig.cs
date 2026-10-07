using UnityEngine;
namespace CombatSystem.Data.Boss
{
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Combat Config", fileName = "BossCombatConfig")]
    public class BossCombatConfig : ScriptableObject
    {
        [Min(1)] public float maxHealth = 500;
        [Min(0)] public float detectionRange = 20;
        [Min(0)] public float attackRange = 15;
        [Min(.1f)] public float patternCooldown = 3f;
        [Min(0)] public float damagePerPlayerHit = 10f;
    }
}
