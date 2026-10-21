using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Combat;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 스킬 A / 스킬 B의 관통 돌진. 돌진하는 동안 적과의 충돌을 무시하고 직선으로 통과하며,
    /// 지나친 적마다 정확히 한 번씩 타격한다.
    /// </summary>
    public sealed class PiercingDashModule : PlayerModule
    {
        #region 필드

        private PlayerProfile.PiercingDashSettings config;
        /// <summary>마지막으로 처리한 공격 시퀀스 번호. 바뀌면 새 돌진으로 보고 상태를 초기화한다.</summary>
        private int sequence = -1;
        private bool started, finished;
        private float elapsed;
        private Vector3 direction;
        /// <summary>돌진 중 충돌을 꺼 둔 적 콜라이더 목록. 종료 시 충돌을 복구한다.</summary>
        private readonly List<Collider> ignored = new List<Collider>();
        /// <summary>이번 돌진에서 이미 맞힌 적/보스. 한 번만 타격하기 위해 기록한다.</summary>
        private readonly HashSet<EnemyDamageReaction> struck = new HashSet<EnemyDamageReaction>();
        private readonly HashSet<CombatSystem.Boss.BossCombat> bosses = new HashSet<CombatSystem.Boss.BossCombat>();
        public bool IsDashing => started && !finished;

        #endregion

        #region 돌진 처리

        /// <summary>프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.piercingDash;
        }

        /// <summary>관통 스킬이 끝나거나 취소되면 꺼 둔 충돌을 복구하고 돌진 상태를 초기화한다.</summary>
        public void Tick()
        {
            if (!Player.Combat.IsUsingPiercingSkill) { RestoreCollisions(); started = false; finished = false; }
        }

        /// <summary>
        /// 관통 스킬 재생 중 루트 모션 단계에서 호출되어 돌진 자체를 구동한다. 시작 진행도에 도달하면 적 충돌을 끄고
        /// 정해진 시간 동안 일정 속도로 이동하며, 지나간 구간을 스윕해 적을 한 번씩 타격한다.
        /// </summary>
        public void TickAnimation()
        {
            var combat = Player.Combat;
            if (!combat.IsUsingPiercingSkill || !character.enabled) return;
            if (sequence != combat.AttackSequence)
            {
                RestoreCollisions(); sequence = combat.AttackSequence;
                started = finished = false; elapsed = 0; struck.Clear(); bosses.Clear();
                direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }
            if (finished) return;
            float dashDistance = combat.IsUsingSkillB ? config.skillBDistance : config.distance;
            float dashDuration = Mathf.Max(.01f, combat.IsUsingSkillB ? config.skillBDuration : config.duration);
            float begin = combat.IsUsingSkillB ? config.skillBStartProgress : config.startProgress;
            string animationState = "Base Layer." + combat.CurrentAttackStateName;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(animationState)) state = next;
            }
            if (!state.IsName(animationState) || state.normalizedTime < begin) return;
            if (!started)
            {
                started = true;
                foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Enemy))
                    foreach (var col in combatant.Transform.GetComponentsInChildren<Collider>())
                        if (col.enabled && !col.isTrigger && !Physics.GetIgnoreCollision(character, col))
                        {
                            Physics.IgnoreCollision(character, col, true); ignored.Add(col);
                        }
            }
            if (Time.deltaTime <= 0) return;
            float step = Mathf.Min(Time.deltaTime, dashDuration - elapsed);
            Vector3 from = transform.position;
            character.Move(direction * (dashDistance / dashDuration * step));
            Vector3 to = transform.position;
            Physics.SyncTransforms();
            // 낮은 FPS에서 크게 이동한 경우까지 포함해 이동한 구간 전체를 스윕하고, 적마다 한 번만 타격한다.
            foreach (var col in Physics.OverlapCapsule(from + Vector3.down * .1f, to + Vector3.down * .1f, config.hitRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var enemy = col.GetComponentInParent<EnemyDamageReaction>();
                if (enemy != null && struck.Add(enemy))
                {
                    enemy.ReceivePiercingHit(transform, direction);
                    combat.RegisterPiercingHit(enemy.transform);
                }
                var boss = col.GetComponentInParent<CombatSystem.Boss.BossCombat>();
                if (boss != null && bosses.Add(boss) && boss.TryReceivePlayerHit()) combat.RegisterPiercingHit(boss.transform);
            }
            elapsed += step;
            if (elapsed >= dashDuration || Vector3.Dot(to - from, direction) < dashDistance / dashDuration * step * .1f)
            {
                finished = true; RestoreCollisions();
            }
        }

        /// <summary>돌진 중 꺼 두었던 적 충돌을 다시 켠다. 적과 겹친 채 멈췄다면 먼저 밀어내 겹침을 해소한다.</summary>
        private void RestoreCollisions()
        {
            // 피격·취소로 적 내부에서 멈춘 경우, 충돌을 되살리기 전에 겹침부터 해소한다.
            foreach (var col in ignored) if (col != null)
            {
                if (character != null && character.enabled && col.enabled)
                {
                    Vector3 away; float depth;
                    if (Physics.ComputePenetration(character, transform.position, transform.rotation,
                        col, col.transform.position, col.transform.rotation, out away, out depth))
                        character.Move(away * (depth + .03f));
                }
                Physics.IgnoreCollision(character, col, false);
            }
            ignored.Clear();
        }

        /// <summary>플레이어 비활성화 시 충돌을 복구하고 돌진을 중단한다.</summary>
        public override void OnDisable() { RestoreCollisions(); started = false; finished = false; }

        #endregion
    }
}
