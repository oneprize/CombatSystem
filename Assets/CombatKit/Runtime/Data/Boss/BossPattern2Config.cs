using UnityEngine;
namespace CombatSystem.Data.Boss
{
    /// <summary>보스 패턴 2(추적 돌진 후 점프 공격)의 속도, 점프 궤적, 충격 범위, 돌진 추적 제한을 정의하는 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Pattern 2 Config", fileName = "BossPattern2Config")]
    public class BossPattern2Config : ScriptableObject
    {
        /// <summary>돌진 전 웅크림 예비 동작 시간(애니메이터가 없을 때의 대체 길이).</summary>
        [Header("Pattern 2 - Charge and Jump")]
        [Min(0)] public float chargeWindup = .7f;
        [Min(.1f)] public float chargeSpeed = 12f;
        [Min(0)] public float jumpStartDistance = 3f;
        [Min(.1f)] public float jumpDuration = .8f;
        [Min(0)] public float jumpHeight = 3f;
        [Min(0)] public float landingRecovery = 1f;
        [Min(.1f)] public float impactRadius = 3f;
        /// <summary>착지 충격이 닿는 보스 기준 수직 허용 범위. 이보다 높거나 낮은 플레이어는 맞지 않는다.</summary>
        [Min(.1f)] public float impactHeight = 2.5f;
        [Header("Charge Tracking")]
        [Min(.1f)] public float maxChargeDistance = 20f;
        /// <summary>돌진 중 조향할 수 있는 최소 회전 반경. 클수록 급선회가 어려워 옆으로 피하기 쉬워진다.</summary>
        [Min(.1f)] public float minTurnRadius = 12f;
        /// <summary>플레이어와 이 거리 이내로 가까워지면 추적을 멈추고 목표를 고정해 점프로 전환한다.</summary>
        [Min(.1f)] public float trackingStopDistance = 5f;
        [Min(.1f)] public float maxChargeDuration = 1.5f;
        /// <summary>점프 애니메이션에서 발이 땅을 떠나는 정규화 시점.</summary>
        [Header("Pattern 2 Animation")]
        [Range(0, .9f)] public float jumpTakeoffTime = .2f;
        /// <summary>점프 애니메이션에서 착지하는 정규화 시점. 이륙 시점보다 최소 0.05 뒤로 보정된다.</summary>
        [Range(.1f, 1)] public float jumpLandingTime = .75f;
        /// <summary>돌진 타격 판정 캡슐의 반지름.</summary>
        [Min(.1f)] public float chargeHitRadius = 2f;
        /// <summary>돌진 타격 판정 캡슐을 보스 발 위치에서 띄우는 높이.</summary>
        public float chargeHitHeight = 2f;
    }
}
