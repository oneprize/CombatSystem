using UnityEngine;

namespace CombatSystem.Data.Enemy
{
    /// <summary>넉백으로 날아가는 적의 초기 속도, 중력, 바닥 충돌 설정을 담는 에셋. EnemyKnockbackMotion이 사용한다.</summary>
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
