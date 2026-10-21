using UnityEngine;

namespace CombatSystem.Data.Enemy
{
    /// <summary>
    /// 적 콤보 1~4타 각각의 무기 히트박스 활성 구간을 담는 설정 에셋.
    /// 각 값은 해당 공격 애니메이션의 정규화 시간 (시작, 끝)이다.
    /// </summary>
    [CreateAssetMenu(menuName = "CombatKit/Enemy/Enemy Weapon Hitbox Config", fileName = "EnemyWeaponHitboxConfig")]
    public class EnemyWeaponHitboxConfig : ScriptableObject
    {
        public Vector2 combo1HitWindow = new Vector2(0.38f, 0.52f);
        public Vector2 combo2HitWindow = new Vector2(0.18f, 0.30f);
        public Vector2 combo3HitWindow = new Vector2(0.03f, 0.16f);
        public Vector2 combo4HitWindow = new Vector2(0.20f, 0.58f);
    }
}
