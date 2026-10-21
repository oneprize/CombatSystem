using UnityEngine;
namespace CombatSystem.Data.Boss
{
    /// <summary>보스의 체력, 탐지/공격 거리, 패턴 간 쿨다운, 그로기 진입 조건과 그로기 중 받는 피해량을 정의하는 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Combat Config", fileName = "BossCombatConfig")]
    public class BossCombatConfig : ScriptableObject
    {
        [Min(1)] public float maxHealth = 500;
        [Min(0)] public float detectionRange = 20;
        [Min(0)] public float attackRange = 15;
        /// <summary>패턴 종료 후 다음 패턴을 고르기까지 대기 상태에서 기다리는 시간(초).</summary>
        [Min(.1f)] public float patternCooldown = 3f;
        /// <summary>그로기 상태로 진입하는 데 필요한 패리 성공 횟수.</summary>
        [Header("Groggy")]
        [Min(1)] public int parriesToGroggy = 4;
        [Min(.1f)] public float groggyDuration = 10f;
        /// <summary>그로기 중 플레이어 공격 1회가 주는 기본 피해량. 공격별 배율이 곱해진다.</summary>
        [Min(0)] public float damagePerPlayerHit = 10f;
    }
}
