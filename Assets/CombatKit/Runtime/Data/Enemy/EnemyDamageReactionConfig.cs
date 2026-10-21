using UnityEngine;

namespace CombatSystem.Data.Enemy
{
    /// <summary>
    /// 적 체력·피격 데미지와 피격 리액션(관통, 지상 콤보 밀어내기, 다운, 공중 바운스, 올려베기) 수치를 담는 설정 에셋.
    /// EnemyDamageReaction이 사용한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CombatKit/Enemy/Enemy Damage Reaction Config", fileName = "EnemyDamageReactionConfig")]
    public class EnemyDamageReactionConfig : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float damagePerHit = 10f;

        /// <summary>관통 돌진 피격 리액션이 A→B→C 단계로 넘어가는 간격(초).</summary>
        [Min(.05f)] public float piercingHitInterval = .12f;

        /// <summary>지상 피격 애니메이션 루트 모션 중 밀려나는 성분에 곱하는 배율.</summary>
        [Min(0)] public float groundReactionMotionScale = .55f;
        /// <summary>지상 콤보 3타 후 공격자와 벌어지도록 맞출 목표 거리.</summary>
        [Header("Ground combo third hit spacing")]
        [Min(0f)] public float thirdHitTargetDistance = 4f;
        /// <summary>목표 거리를 맞추기 위해 추가로 밀어낼 수 있는 최대 거리.</summary>
        [Min(0f)] public float thirdHitMaxExtraPush = 3.4f;
        [Min(.01f)] public float thirdHitPushDuration = .2f;

        /// <summary>다운 후 바닥에 누워 있다가 기상하기까지의 시간(초).</summary>
        [Min(0)] public float downDuration = 1f;

        [Min(0f)] public float aerialHitBlendTime = 0.1f;

        [Header("Air finisher follow-up")]
        [Min(0f)] public float slamBounceHeight = .8f;
        /// <summary>착지 바운스 후 추가타를 이어 받을 수 있는 시간(초).</summary>
        [Min(.1f)] public float slamFollowUpWindow = .8f;

        [Header("Upper attack reaction")]
        [Min(0)] public float upperLiftSpeed = 8f;
        [Min(0)] public float upperPushSpeed = 1.5f;
    }
}
