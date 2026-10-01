using UnityEngine;

namespace CombatSystem.Data.Enemy
{
    [CreateAssetMenu(menuName = "CombatKit/Enemy/Enemy Weapon Hitbox Config", fileName = "EnemyWeaponHitboxConfig")]
    public class EnemyWeaponHitboxConfig : ScriptableObject
    {
        public Vector2 combo1HitWindow = new Vector2(0.38f, 0.52f);
        public Vector2 combo2HitWindow = new Vector2(0.18f, 0.30f);
        public Vector2 combo3HitWindow = new Vector2(0.03f, 0.16f);
        public Vector2 combo4HitWindow = new Vector2(0.20f, 0.58f);
    }
}
