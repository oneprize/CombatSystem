using UnityEngine;

namespace CombatSystem.Data.Enemy
{
    [CreateAssetMenu(menuName = "CombatKit/Enemy/Enemy Damage Reaction Config", fileName = "EnemyDamageReactionConfig")]
    public class EnemyDamageReactionConfig : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float damagePerHit = 10f;

        [Min(.05f)] public float piercingHitInterval = .12f;

        [Min(0)] public float groundReactionMotionScale = .55f;

        [Min(0)] public float downDuration = 1f;

        [Min(0f)] public float aerialHitBlendTime = 0.1f;

        [Header("Upper attack reaction")]
        [Min(0)] public float upperLiftSpeed = 8f;
        [Min(0)] public float upperPushSpeed = 1.5f;
    }
}
