using UnityEngine;

namespace CombatSystem.Data.Enemy
{
    [CreateAssetMenu(menuName = "CombatKit/Enemy/Enemy Knockback Motion Config", fileName = "EnemyKnockbackMotionConfig")]
    public class EnemyKnockbackMotionConfig : ScriptableObject
    {
        [Min(0)] public float launchSpeed = 6f;
        [Min(0)] public float upwardSpeed = 4f;
        [Min(.1f)] public float gravity = 18f;
        [Min(0)] public float groundClearance = .02f;
        public LayerMask collisionMask = ~0;
    }
}
