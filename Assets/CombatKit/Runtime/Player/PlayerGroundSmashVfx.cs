using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Q 스킬의 두 번째 타격 시점에 지면 바위 폭발 이펙트를 공격당 한 번 생성한다.
    /// 타이밍은 히트박스 설정의 스킬 히트 구간을 그대로 써서 실제 판정과 맞춘다.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed class PlayerGroundSmashVfx : MonoBehaviour
    {
        #region 필드

        [SerializeField] private PlayerWeaponHitboxConfig hitConfig;
        [SerializeField] private GameObject effectPrefab;
        [SerializeField, Min(.01f)] private float effectScale = .32f;
        [SerializeField, Min(.1f)] private float effectLifetime = 4f;
        [SerializeField, Range(0f, 1f)] private float effectStartTime = .0842f;
        [SerializeField] private LayerMask groundLayers = ~0;
        private PlayerCharacter player;
        private Animator animator;
        private int sequence = -1;
        private bool played;
        /// <summary>지금까지 생성한 이펙트 수. 디버그/테스트 확인용.</summary>
        public int PlayedCount { get; private set; }

        #endregion

        #region Unity 생명주기

        /// <summary>플레이어와 애니메이터 참조를 캐시한다.</summary>
        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
        }

        /// <summary>Q 스킬 애니메이션이 두 번째 타격 구간에 들어서는 순간을 감지해 이펙트를 한 번만 생성한다.</summary>
        private void LateUpdate()
        {
            if (player == null || player.Combat == null || animator == null ||
                hitConfig == null || effectPrefab == null) return;
            var combat = player.Combat;
            if (sequence != combat.AttackSequence)
            {
                sequence = combat.AttackSequence;
                played = false;
            }
            if (played || !combat.IsUsingSkill || !combat.IsAttacking || player.IsHitReacting) return;
            string stateName = "Base Layer." + combat.CurrentAttackStateName;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(stateName)) state = next;
            }
            if (!state.IsName(stateName) || state.normalizedTime < hitConfig.skillHitWindow.x) return;
            // 이 이펙트는 Q 스킬의 두 번째 타격에 속하며 공격당 한 번만 재생한다.
            played = true;
            SpawnImpact();
        }

        #endregion

        #region 이펙트 생성

        /// <summary>
        /// 스킬 충돌 지점 아래로 레이를 쏴서 플레이어/적/보스를 제외한 가장 가까운 평평한 지면을 찾고,
        /// 그 지면 법선에 맞춰 이펙트를 배치한 뒤 수명이 지나면 제거한다. 지면이 없으면 생성하지 않는다.
        /// </summary>
        private void SpawnImpact()
        {
            Vector3 center = transform.TransformPoint(hitConfig.skillImpactCenter);
            var hits = Physics.RaycastAll(center + Vector3.up * 3f, Vector3.down, 8f,
                groundLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit ground = default;
            foreach (var hit in hits)
            {
                if (hit.normal.y < .5f || hit.distance >= nearest ||
                    hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>() != null ||
                    hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() != null) continue;
                nearest = hit.distance;
                ground = hit;
            }
            if (float.IsPositiveInfinity(nearest)) return;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, ground.normal).normalized;
            // FBX의 애니메이션 루트 트랜스폼이 월드 배치를 덮어쓰지 않도록 별도 앵커 아래에 둔다.
            var anchor = new GameObject("Q Ground Smash");
            anchor.transform.SetPositionAndRotation(ground.point + ground.normal * .015f,
                Quaternion.LookRotation(forward, ground.normal));
            anchor.transform.localScale = Vector3.one * effectScale;
            var effect = Instantiate(effectPrefab, anchor.transform, false);
            var effectAnimator = effect.GetComponent<Animator>();
            if (effectAnimator != null)
            {
                // FBX의 예비 동작 구간을 건너뛰어 접촉 순간에 바로 바위가 터지게 한다.
                effectAnimator.Play("GroundSmash", 0, effectStartTime);
                effectAnimator.Update(0f);
            }
            PlayedCount++;
            Destroy(anchor, effectLifetime);
        }

        #endregion
    }
}
