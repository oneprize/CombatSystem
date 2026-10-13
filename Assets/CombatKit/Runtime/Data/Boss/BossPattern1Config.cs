using UnityEngine;
namespace CombatSystem.Data.Boss
{
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Pattern 1 Config", fileName = "BossPattern1Config")]
    public class BossPattern1Config : ScriptableObject
    {
        [Header("Pattern 1 - Projectile")]
        public Vector3 projectileSpawnOffset = new Vector3(0, 1.5f, 1.2f);
        [Min(.1f)] public float projectileSpeed = 12f;
        [Min(.05f)] public float projectileDiameter = .6f;
        [Min(.1f)] public float projectileLifetime = 8f;
        [Range(0f, 1f)] public float throwReleaseProgress = 49f / 115f;
    }
}
