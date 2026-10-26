using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 공중 콤보의 체공·낙하·착지와 공중 대시 공격, UpperAttack 띄우기와 ChaseUpAttack 추격 도약의
    /// 스크립트 이동을 담당한다. 공중 전투 중에는 적 몸체 위에 올라서지 않도록 적 충돌도 관리한다.
    /// </summary>
    public sealed partial class AirComboModule : PlayerModule
    {
        #region 필드

        /// <summary>공중 콤보(체공~낙하~착지)가 진행 중인지. 이 동안 높이는 이 모듈이 소유한다.</summary>
        public bool Active { get; private set; }
        public bool IsAirDashing { get; private set; }
        private float dashElapsed, dashTravel, dashEndWait;
        /// <summary>공중 대시 중 공격 입력이 들어와 후속 공격을 예약했는지.</summary>
        private bool dashAttackQueued;
        /// <summary>공중 대시로 처음 맞힌 공중 상태의 적. 후속 공격의 대상이 된다.</summary>
        private CombatSystem.Combat.EnemyDamageReaction dashTarget;
        private readonly HashSet<CombatSystem.Combat.EnemyDamageReaction> dashHits = new HashSet<CombatSystem.Combat.EnemyDamageReaction>();
        private readonly HashSet<CombatSystem.Boss.BossCombat> dashBossHits = new HashSet<CombatSystem.Boss.BossCombat>();

        #endregion

        #region 공중 대시

        /// <summary>
        /// 공중에서 공중 대시 공격을 시작한다. 실제 발밑이 바닥과 충분히 떨어져 있을 때만 허용한다.
        /// </summary>
        /// <returns>지상이거나 다른 동작(피격·회피·링크 점프·가드) 중이거나 사망 상태면 false.</returns>
        public bool TryBeginAirDash()
        {
            if (IsAirDashing || !character.enabled || Player.IsHitReacting || Player.IsDodging ||
                Player.IsLinkJumping || Player.IsGuarding || Player.Health.CurrentHealth <= 0 || character.isGrounded)
                return false;
            // UpperAttack 중에는 이동 모듈이 캐싱한 접지 플래그가 갱신되지 않으므로, 실제 바닥과의 간격을 직접 확인한다.
            foreach (var hit in Physics.RaycastAll(character.bounds.center, Vector3.down,
                character.bounds.extents.y + .2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>() != null ||
                    hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() != null) continue;
                if (hit.normal.y > .5f) return false;
            }
            foreach (var state in new[] { "AirDashAttack" })
                if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) return false;
            Player.Combat.BeginAirDash();
            Begin();
            IsAirDashing = true; dashElapsed = dashTravel = dashEndWait = 0; previousDashProgress = 0;
            dashAttackQueued = false; dashTarget = null;
            dashHits.Clear(); dashBossHits.Clear();
            TickEnemyCollision();
            animator.CrossFadeInFixedTime("Base Layer.AirDashAttack", .06f, 0, 0);
            return true;
        }

        /// <summary>공중 대시를 중단하고 곧바로 낙하로 전환한다.</summary>
        public void CancelAirDash()
        {
            if (!IsAirDashing) return;
            IsAirDashing = false;
            falling = false;
            Fall();
        }

        /// <summary>
        /// 이번 프레임 대시 이동 구간을 스윕해 닿은 적을 한 번씩 타격한다. 공중의 적은 공중 콤보 타격,
        /// 지상의 적은 지상 콤보 타격으로 처리하며, 벽 너머의 적은 맞히지 않는다.
        /// </summary>
        private void DashContacts(Vector3 from, Vector3 to)
        {
            float radius = .9f;
            foreach (var other in Physics.OverlapCapsule(from + Vector3.up * .5f, to + Vector3.up * .5f,
                radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(transform)) continue;
                Vector3 point = other.ClosestPoint(to + Vector3.up * .5f);
                Vector3 direction = point - (to + Vector3.up * .5f);
                // 겹침 판정이 아레나 벽을 관통해 적을 맞히지 않도록 사이에 벽이 있는지 확인한다.
                bool blocked = false;
                foreach (var wall in Physics.RaycastAll(to + Vector3.up * .5f, direction.normalized,
                    direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (wall.transform.IsChildOf(transform) || wall.collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>() != null ||
                        wall.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() != null) continue;
                    blocked = true; break;
                }
                if (blocked) continue;
                var enemy = other.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>();
                if (enemy != null && enemy.CurrentHealth > 0 && dashHits.Add(enemy))
                {
                    if (enemy.IsAirborneForCombo)
                    {
                        enemy.ReceiveAirComboHit(transform, 0, false);
                        if (dashTarget == null) dashTarget = enemy;
                    }
                    else enemy.ReceiveGroundComboHit(transform, 1, false);
                }
                var boss = other.GetComponentInParent<CombatSystem.Boss.BossCombat>();
                if (boss != null && dashBossHits.Add(boss)) boss.TryReceivePlayerHit();
            }
        }

        /// <summary>직전 프레임의 대시 애니메이션 진행도. 타격 구간(.15~.7) 판정에 쓴다.</summary>
        private float previousDashProgress;
        /// <summary>AirDashAttack으로 전이 중이면 다음 상태를, 아니면 현재 상태 정보를 돌려준다.</summary>
        private AnimatorStateInfo DashState()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("AirDashAttack"))
                state = animator.GetNextAnimatorStateInfo(0);
            return state;
        }
        /// <summary>
        /// 공중 대시 클립의 수평 루트 모션을 배율을 적용해 최대 거리까지만 이동시키고, 타격 구간에서는 지나간 경로의 적을 판정한다.
        /// </summary>
        public void ApplyAirDashRootMotion(Vector3 delta)
        {
            if (!IsAirDashing || !character.enabled) return;
            var state = DashState();
            if (!state.IsName("AirDashAttack")) return;
            delta.y = 0f;
            Vector3 before = transform.position;
            delta *= config.airDashMotionScale;
            delta = Vector3.ClampMagnitude(delta, Mathf.Max(0, config.airDashMaxDistance - dashTravel));
            character.Move(delta);
            dashTravel += Vector3.Distance(before, transform.position);
            if (state.normalizedTime >= .15f && previousDashProgress <= .7f)
                DashContacts(before, transform.position);
            previousDashProgress = state.normalizedTime;
        }
        /// <summary>
        /// 공중 대시 진행 중 공격 입력을 예약해 두었다가 후속 공격 가능 시점에 연계하고, 입력이 없으면 유예 시간 뒤 낙하로 전환한다.
        /// </summary>
        private void TickAirDash(float dt)
        {
            dashElapsed += dt;
            if (Player.Controls.AttackPressed) dashAttackQueued = true;
            var state = DashState();
            if (dashAttackQueued && state.IsName("AirDashAttack") && state.normalizedTime >= config.airDashFollowUpProgress)
            {
                IsAirDashing = false;
                Begin();
                Player.Combat.BeginAirDashFollowUp(dashTarget);
                return;
            }
            if (state.IsName("AirDashAttack") && state.normalizedTime >= 1f) dashEndWait += dt;
            if (dashEndWait >= config.airDashFollowUpGrace || dashElapsed >= 2f)
            {
                IsAirDashing = false;
                Player.Combat.FinishAirDash();
                Fall();
            }
        }

        #endregion

        #region 공중 콤보 상태

        /// <summary>공중 콤보가 끝나 낙하·착지 중인지.</summary>
        public bool Recovering => Active && falling;
        private PlayerProfile.AirComboSettings config;
        /// <summary>falling: 낙하 중, landing: 착지 애니메이션 중, fastFall: 피니시 후 빠른 낙하(착지 연계 공격 허용).</summary>
        private bool falling, landing, fastFall;
        /// <summary>피니시 후 빠른 낙하 중이라 착지 순간 지상 공격으로 연계할 수 있는지.</summary>
        public bool AllowsLandingFollowUp => fastFall && Recovering;
        private float verticalSpeed, heldTime;
        private float previousUpperHeight;
        private readonly List<Collider> ignoredEnemyColliders = new List<Collider>();
        private bool ignoringEnemies;
        // 원본 UpperAttack에서 30fps로 샘플링한 발 높이 궤적.
        // 단독 휴머노이드 클립의 Bake 플래그는 에셋을 다시 불러오면 유지되지 않을 때가 있어 코드로 높이를 제어한다.
        private static readonly float[] UpperHeights = { 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.85341f, 1.11443f, 1.16875f, 1.22002f, 1.26748f, 1.30045f, 1.32557f, 1.38839f, 1.50463f, 1.60975f, 1.66568f, 1.71637f, 1.76262f, 1.80183f, 1.83267f, 1.85791f, 1.88061f, 1.90397f, 1.93004f, 1.83954f, 1.66474f, 1.45540f, 1.23242f, 0.66809f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f };

        #endregion

        #region UpperAttack 및 적 충돌

        /// <summary>프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.airCombo;
        }

        /// <summary>UpperAttack 시작 시 높이 궤적 추적을 0부터 다시 시작하게 한다.</summary>
        public void BeginUpperMotion() { previousUpperHeight = 0; }

        /// <summary>
        /// 전투 중 공중에 있는 동안(UpperAttack, 공중 콤보와 그 낙하, 추격 공격, 파워 공격) CharacterController가
        /// 적 몸체를 무시해 적 위에 올라서지 않게 한다. 타격 판정은 겹침 쿼리라 IgnoreCollision의 영향을 받지 않는다.
        /// 착지 후에는 서로 겹치지 않게 된 쌍부터 충돌을 복구해, 플레이어가 적 밖으로 튕겨 나가지 않게 한다.
        /// </summary>
        public void TickEnemyCollision()
        {
            if (Active || Player.Combat.IsUsingUpperAttack || Player.Combat.IsUsingChaseAttack || Player.Combat.IsReleasingPower)
            {
                if (ignoringEnemies) return;
                ignoringEnemies = true;
                foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Enemy))
                    foreach (var col in combatant.Transform.GetComponentsInChildren<Collider>())
                        if (col.enabled && !col.isTrigger && !Physics.GetIgnoreCollision(character, col))
                        {
                            Physics.IgnoreCollision(character, col, true);
                            ignoredEnemyColliders.Add(col);
                        }
                return;
            }
            ignoringEnemies = false;
            for (int i = ignoredEnemyColliders.Count - 1; i >= 0; i--)
            {
                var col = ignoredEnemyColliders[i];
                if (col != null && col.enabled && col.gameObject.activeInHierarchy && character.enabled &&
                    Physics.ComputePenetration(character, transform.position, transform.rotation,
                        col, col.transform.position, col.transform.rotation, out _, out _))
                    continue;
                if (col != null) Physics.IgnoreCollision(character, col, false);
                ignoredEnemyColliders.RemoveAt(i);
            }
        }

        /// <summary>겹침 여부와 관계없이 무시하던 적 충돌을 즉시 모두 복구한다(비활성화 시 사용).</summary>
        private void RestoreEnemyCollisions()
        {
            foreach (var col in ignoredEnemyColliders)
                if (col != null) Physics.IgnoreCollision(character, col, false);
            ignoredEnemyColliders.Clear();
            ignoringEnemies = false;
        }

        /// <summary>UpperAttack의 수평 루트 모션은 그대로 쓰고, 높이는 샘플링한 발 궤적을 따라 상승시킨다.</summary>
        public void ApplyUpperMotion(Vector3 rootDelta)
        {
            if (!character.enabled) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("UpperAttack"))
                state = animator.GetNextAnimatorStateInfo(0);
            if (!state.IsName("UpperAttack")) return;
            float sample = Mathf.Clamp01(state.normalizedTime) * (UpperHeights.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(sample), UpperHeights.Length - 2);
            float height = Mathf.Lerp(UpperHeights[index], UpperHeights[index + 1], sample - index);
            rootDelta.y = height - previousUpperHeight;
            previousUpperHeight = height;
            character.Move(rootDelta);
        }

        #endregion

        #region 추격 도약

        // ChaseUpAttack 진행도 5%마다 도약이 얼마나 진행됐는지를 원본 클립(Attack_Up_Floor_To_Air_02)에서 샘플링한 값.
        // 40%까지 웅크리며 다가가고, 그 뒤 도약해 90%에서 정점에 도달한다.
        private static readonly float[] ChaseRise = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, .22f, .39f, .54f, .68f, .80f, .88f, .94f, .97f, .99f, 1f, 1f, 1f };
        private static readonly float[] ChaseTravel = { 0f, .05f, .05f, .10f, .31f, .45f, .59f, .74f, .85f, .88f, .91f, .95f, .96f, .96f, .97f, .97f, .98f, .99f, 1f, 1f, 1f };
        private Vector3 chaseStart, chaseEnd;

        /// <summary>
        /// 지상에서 띄워진 적에게 날아오르는 추격 도약의 시작·도착점을 계산한다. 도착점은 적과 발 높이가 같고
        /// 적 앞 <paramref name="contactDistance"/> 지점이며, 이미 그보다 가까우면 수평 위치는 그대로 둔다.
        /// </summary>
        public void BeginChaseMotion(Transform target, float contactDistance)
        {
            chaseStart = transform.position;
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            Vector3 approach = distance > contactDistance ? toTarget / distance * (distance - contactDistance) : Vector3.zero;
            float feetToPivot = transform.position.y - character.bounds.min.y;
            chaseEnd = new Vector3(chaseStart.x + approach.x, target.position.y + feetToPivot, chaseStart.z + approach.z);
            if (chaseEnd.y < chaseStart.y) chaseEnd.y = chaseStart.y;
        }

        /// <summary>ChaseUpAttack 진행도에 따라 샘플링한 상승·이동 비율로 시작점과 도착점 사이를 보간해 이동시킨다.</summary>
        public void ApplyChaseMotion()
        {
            if (!character.enabled) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("ChaseUpAttack"))
                state = animator.GetNextAnimatorStateInfo(0);
            if (!state.IsName("ChaseUpAttack")) return;
            float rise = Sample(ChaseRise, state.normalizedTime), travel = Sample(ChaseTravel, state.normalizedTime);
            Vector3 desired = new Vector3(Mathf.Lerp(chaseStart.x, chaseEnd.x, travel),
                Mathf.Lerp(chaseStart.y, chaseEnd.y, rise), Mathf.Lerp(chaseStart.z, chaseEnd.z, travel));
            character.Move(desired - transform.position);
        }

        /// <summary>균등 간격으로 샘플링한 커브를 정규화 시간으로 선형 보간해 읽는다.</summary>
        private static float Sample(float[] curve, float normalizedTime)
        {
            float sample = Mathf.Clamp01(normalizedTime) * (curve.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(sample), curve.Length - 2);
            return Mathf.Lerp(curve[index], curve[index + 1], sample - index);
        }

        #endregion

        #region 체공·낙하·착지

        /// <summary>공중 콤보를 새로 시작할 수 있는지 확인한다. 공중에 있고 발밑 가까이에 바닥이 없어야 한다.</summary>
        public bool CanBegin()
        {
            if (Active || !character.enabled || character.isGrounded) return false;
            var bottom = character.bounds.center - Vector3.up * character.bounds.extents.y;
            return !Physics.Raycast(bottom + Vector3.up * .05f, Vector3.down, .2f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>공중 콤보 체공을 시작한다. 이후 높이는 이 모듈이 유지한다.</summary>
        public void Begin()
        {
            Active = true; fastFall = falling = landing = false; verticalSpeed = heldTime = 0;
        }

        /// <summary>체공을 끝내고 낙하로 전환한다. fast면 피니시 후 빠른 낙하로, 착지 연계 공격을 허용한다.</summary>
        public void Fall(bool fast = false)
        {
            if (!Active || falling) return;
            falling = true; fastFall = fast; verticalSpeed = fast ? -config.finisherFallSpeed : 0f;
            if (!Player.IsHitReacting) animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop", .1f, 0, 0);
        }

        /// <summary>빠른 낙하 후 착지했을 때 예약된 지상 공격으로 연계를 시도한다. 실패하면 공중 콤보 상태를 되돌린다.</summary>
        /// <returns>착지 연계 공격이 시작되었으면 true.</returns>
        private bool TryLandingAttack()
        {
            if (!fastFall || Player.IsHitReacting) return false;
            Active = false;
            Player.Locomotion.OnCombatLanded();
            if (Player.Combat.TryLandingFollowUp()) { fastFall = false; return true; }
            Active = true;
            return false;
        }

        /// <summary>
        /// 공중 콤보 진행을 갱신한다. 체공 중에는 공중 공격이 끊기거나 최대 체공 시간이 지나면 콤보를 끝내고,
        /// 낙하 중에는 작은 스텝으로 중력을 적용해 착지를 감지하며, 착지 애니메이션이 끝나면 대기 상태로 돌아간다.
        /// </summary>
        public void Tick(float dt)
        {
            if (!Active) return;
            if (IsAirSlamming) { TickAirSlam(dt); return; }
            if (IsAirDashing) { TickAirDash(dt); return; }
            if (!falling)
            {
                heldTime += dt;
                if (!Player.Combat.IsUsingAirCombo || heldTime >= config.maximumHoldTime) Player.Combat.EndAirCombo();
                return;
            }
            if (landing)
            {
                if (TryLandingAttack()) return;
                if (Player.IsHitReacting) { Active = false; return; }
                if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("LinkJumpEnd") &&
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
                { Active = false; animator.CrossFadeInFixedTime("Base Layer.Idle", .1f); }
                return;
            }
            float remaining = dt;
            while (remaining > .00001f)
            {
                float step = Mathf.Min(.02f, remaining); remaining -= step;
                verticalSpeed -= (fastFall ? config.finisherFallGravity : config.fallGravity) * step;
                var collision = character.Move(Vector3.up * verticalSpeed * step);
                if ((collision & CollisionFlags.Below) != 0)
                {
                    verticalSpeed = 0; landing = true;
                    Player.Locomotion.OnCombatLanded();
                    if (TryLandingAttack()) return;
                    if (Player.IsHitReacting) Active = false;
                    else animator.CrossFadeInFixedTime("Base Layer.LinkJumpEnd", .1f, 0, 0);
                    break;
                }
            }
        }

        /// <summary>플레이어 비활성화 시 공중 대시·공중 콤보 상태를 모두 해제하고 적 충돌을 복구한다.</summary>
        public override void OnDisable() { slamPhase = SlamPhase.None; slamHits.Clear(); slamBossHits.Clear(); IsAirDashing = false; Active = fastFall = falling = landing = false; RestoreEnemyCollisions(); }

        #endregion
    }
}
