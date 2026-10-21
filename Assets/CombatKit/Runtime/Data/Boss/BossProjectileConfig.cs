using UnityEngine;
namespace CombatSystem.Data.Boss
{
    /// <summary>보스 투사체의 패리 창(발동 거리, 지속 시간, 슬로 모션 배율)과 패리 성공 시 파편 연출을 정의하는 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Projectile Config", fileName = "BossProjectileConfig")]
    public class BossProjectileConfig : ScriptableObject
    {
        /// <summary>투사체 표면과 플레이어 몸통 사이 거리가 이 값 이하가 되면 패리 창이 열린다.</summary>
        [Min(.1f)] public float parryDistance = 4f;
        /// <summary>패리 창 지속 시간(실제 시간 기준 초).</summary>
        [Min(.1f)] public float parryDuration = 2f;
        /// <summary>패리 창 동안 적용할 최대 시간 배율. 투사체가 창 안에 닿지 않도록 더 느려질 수 있다.</summary>
        [Range(.001f, 1)] public float slowScale = .03f;
        /// <summary>패리 성공 시 각 파편에 가하는 바깥 방향 충격량.</summary>
        [Min(0)] public float fragmentImpulse = 4f;
        [Min(.1f)] public float fragmentLifetime = 4f;
    }
}
