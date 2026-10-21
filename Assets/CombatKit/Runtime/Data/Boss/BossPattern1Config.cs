using UnityEngine;
namespace CombatSystem.Data.Boss
{
    /// <summary>보스 패턴 1(투사체 던지기)의 발사 위치, 속도, 수명, 릴리스 타이밍을 정의하는 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Pattern 1 Config", fileName = "BossPattern1Config")]
    public class BossPattern1Config : ScriptableObject
    {
        /// <summary>손 본이나 생성 지점이 없을 때 사용하는 보스 로컬 기준 발사 위치.</summary>
        [Header("Pattern 1 - Projectile")]
        public Vector3 projectileSpawnOffset = new Vector3(0, 1.5f, 1.2f);
        [Min(.1f)] public float projectileSpeed = 12f;
        [Min(.05f)] public float projectileDiameter = .6f;
        [Min(.1f)] public float projectileLifetime = 8f;
        /// <summary>Throw 애니메이션에서 투사체가 손을 떠나는 정규화 진행도(기본값은 115프레임 중 49프레임).</summary>
        [Range(0f, 1f)] public float throwReleaseProgress = 49f / 115f;
    }
}
