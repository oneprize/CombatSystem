using UnityEngine;
namespace CombatSystem.Data.Boss
{
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Projectile Config", fileName = "BossProjectileConfig")]
    public class BossProjectileConfig : ScriptableObject
    {
        [Min(.1f)] public float parryDistance = 4f;
        [Min(.1f)] public float parryDuration = 2f;
        [Range(.001f, 1)] public float slowScale = .03f;
        [Min(0)] public float fragmentImpulse = 4f;
        [Min(.1f)] public float fragmentLifetime = 4f;
    }
}
