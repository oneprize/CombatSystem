using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Combat;
using CombatSystem.Core;

namespace CombatSystem.Player
{
    public sealed partial class AirComboModule
    {
        private enum SlamPhase { None, Start, Fall, Land }
        private SlamPhase slamPhase;
        private float slamPhaseTime;
        private bool slamHasDestination;
        private Vector3 slamDestination;
        private float slamMoveSpeed, slamFallGravity;
        private readonly HashSet<EnemyDamageReaction> slamHits = new HashSet<EnemyDamageReaction>();
        private readonly HashSet<CombatSystem.Boss.BossCombat> slamBossHits = new HashSet<CombatSystem.Boss.BossCombat>();

        public bool IsAirSlamming => slamPhase != SlamPhase.None;
        public bool IsSlamFalling => slamPhase == SlamPhase.Fall;
        public bool IsSlamLanding => slamPhase == SlamPhase.Land;
        public bool SlamTrailActive => IsSlamFalling || (IsSlamLanding && slamPhaseTime < .12f);
        public string SlamStateName => slamPhase == SlamPhase.Start ? "AirSlamStart" :
            slamPhase == SlamPhase.Fall ? "AirSlamLoop" : "AirSlamEnd";

        /// <summary>공중 Q. 점프·어퍼·공중 콤보·낙하 중의 이동을 내려찍기가 이어받는다.</summary>
        public bool TryBeginAirSlam()
        {
            if (IsAirSlamming || !character.enabled || Player.IsDead || Player.IsHitReacting ||
                Player.IsDodging || Player.IsLinkJumping || Player.IsGuarding ||
                Player.Combat.IsChargingPower || Player.Combat.IsParryAction ||
                (Player.WeaponStance != null && (!Player.WeaponStance.IsDrawn || Player.WeaponStance.IsTransitioning)) ||
                (Player.LedgeHang != null && Player.LedgeHang.IsHanging) ||
                (Player.Hang != null && Player.Hang.IsHanging) ||
                (Player.WallRun != null && Player.WallRun.IsWallRunning)) return false;

            // UpperAttack caches locomotion grounding, so test the actual capsule clearance.
            Vector3 feet = character.bounds.center - Vector3.up * character.bounds.extents.y;
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * .1f, Vector3.down, .25f,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsSlamCombatCollider(hit.collider)) continue;
                if (hit.normal.y > .5f) return false;
            }
            foreach (string state in new[] { "AirSlamStart", "AirSlamLoop", "AirSlamEnd" })
                if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) return false;

            Transform target = SelectSlamTarget();
            Player.Combat.BeginAirSlam(target);
            Begin();
            // Snapshot once: moving enemies do not steer the player throughout the dive.
            slamHasDestination = target != null;
            slamDestination = target != null ? target.position : transform.position;
            slamMoveSpeed = 0f;
            slamHits.Clear();
            slamBossHits.Clear();
            SetSlamPhase(SlamPhase.Start, .06f);
            TickEnemyCollision();
            return true;
        }

        private Transform SelectSlamTarget()
        {
            float range = Mathf.Max(0f, Player.Profile.combat.skillTargetDistance);
            if (range <= 0f) return null;
            float rangeSquared = range * range;
            var locked = Player.LockOn.Target;
            if (IsValidSlamTarget(locked, rangeSquared)) return locked.Transform;

            Transform nearest = null;
            foreach (var candidate in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                if (!IsValidSlamTarget(candidate, rangeSquared)) continue;
                Vector3 direction = candidate.Transform.position - transform.position;
                direction.y = 0f;
                rangeSquared = direction.sqrMagnitude;
                nearest = candidate.Transform;
            }
            return nearest;
        }

        private bool IsValidSlamTarget(ICombatant candidate, float rangeSquared)
        {
            if (candidate == null || candidate.Transform == null || candidate.Team != CombatTeam.Enemy ||
                !candidate.Transform.gameObject.activeInHierarchy) return false;
            var enemy = candidate.Transform.GetComponent<EnemyDamageReaction>();
            var boss = candidate.Transform.GetComponent<CombatSystem.Boss.BossCombat>();
            if (enemy != null ? (!enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0f) :
                (boss == null || !boss.isActiveAndEnabled || boss.CurrentHealth <= 0f)) return false;
            Vector3 direction = candidate.Transform.position - transform.position;
            if (direction.y > Mathf.Max(0f, config.slamTargetHeightAbove) ||
                direction.y < -Mathf.Max(0f, config.slamTargetDepth)) return false;
            direction.y = 0f;
            // Directly below is also a valid target (zero horizontal distance).
            return direction.sqrMagnitude <= rangeSquared;
        }

        private void BeginSlamFall()
        {
            float speed = Mathf.Max(.1f, config.slamFallSpeed);
            slamFallGravity = Mathf.Max(.1f, config.slamGravity);
            verticalSpeed = -speed;
            slamMoveSpeed = 0f;
            if (!slamHasDestination) return;

            Vector3 offset = slamDestination - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < .001f) return;

            // Find the floor below the saved target, ignoring characters (including airborne enemies).
            float groundY = slamDestination.y;
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(slamDestination + Vector3.up * .5f, Vector3.down,
                Mathf.Max(1f, config.slamTargetDepth + 1f), ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsSlamCombatCollider(hit.collider) || hit.normal.y <= .5f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                groundY = hit.point.y;
            }
            slamMoveSpeed = Mathf.Max(.1f, config.slamHorizontalSpeed);
            // Without a floor estimate, keep normal gravity and only move toward the saved XZ.
            if (float.IsPositiveInfinity(nearest)) return;
            float feetY = character.bounds.min.y;
            float height = Mathf.Max(.01f, feetY - groundY - character.skinWidth);
            // Stable positive root of height = speed * time + gravity * time^2 / 2.
            float fallTime = 2f * height / (Mathf.Sqrt(speed * speed + 2f * slamFallGravity * height) + speed);
            float travelTime = Mathf.Max(fallTime, distance / slamMoveSpeed);
            float timeScale = fallTime / travelTime;
            // Stretch the descent to match the capped horizontal travel, without a hover or teleport.
            verticalSpeed *= timeScale;
            slamFallGravity *= timeScale * timeScale;
            slamMoveSpeed = distance / travelTime;
        }

        private void SetSlamPhase(SlamPhase phase, float blend)
        {
            slamPhase = phase;
            slamPhaseTime = 0f;
            animator.CrossFadeInFixedTime("Base Layer." + SlamStateName, blend, 0, 0f);
        }

        private AnimatorStateInfo SlamState()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(SlamStateName)) state = next;
            }
            return state;
        }

        private void TickAirSlam(float dt)
        {
            if (Player.IsDead || Player.IsHitReacting || !character.enabled)
            {
                Player.Combat.CancelForDamage();
                return;
            }
            slamPhaseTime += dt;
            var state = SlamState();
            if (slamPhase == SlamPhase.Start)
            {
                if ((state.IsName("AirSlamStart") && state.normalizedTime >= .9f) || slamPhaseTime >= .65f)
                {
                    BeginSlamFall();
                    SetSlamPhase(SlamPhase.Fall, .05f);
                }
                return;
            }
            if (slamPhase == SlamPhase.Land)
            {
                bool canChain = slamPhaseTime >= config.slamLandingRecovery && Player.Combat.GroundAttackInputReady;
                if (canChain || (state.IsName("AirSlamEnd") && state.normalizedTime >= 1f) || slamPhaseTime >= 3f)
                {
                    slamPhase = SlamPhase.None;
                    Active = fastFall = falling = landing = false;
                    Player.Locomotion.OnCombatLanded();
                    Player.Combat.FinishAirSlam();
                    if (!Player.Combat.TryLandingFollowUp())
                        animator.CrossFadeInFixedTime("Base Layer.Idle", .12f, 0, 0f);
                }
                return;
            }

            // Move the capsule in short steps; animation root Y must not add a second fall.
            float remaining = dt;
            while (remaining > .00001f && IsSlamFalling)
            {
                float step = Mathf.Min(.02f, remaining);
                remaining -= step;
                Vector3 before = character.bounds.center;
                Vector3 horizontal = slamHasDestination ? slamDestination - transform.position : Vector3.zero;
                horizontal.y = 0f;
                horizontal = Vector3.ClampMagnitude(horizontal, slamMoveSpeed * step);
                float drop = verticalSpeed * step - .5f * slamFallGravity * step * step;
                verticalSpeed -= slamFallGravity * step;
                var collision = character.Move(horizontal + Vector3.up * drop);
                if ((collision & CollisionFlags.Sides) != 0)
                {
                    // A wall cancels the approach; resume normal gravity instead of sliding along it.
                    slamHasDestination = false;
                    verticalSpeed = Mathf.Min(verticalSpeed, -Mathf.Max(.1f, config.slamFallSpeed));
                    slamFallGravity = Mathf.Max(.1f, config.slamGravity);
                }
                HitSlamContacts(before, character.bounds.center, false);
                if ((collision & CollisionFlags.Below) == 0) continue;
                verticalSpeed = 0f;
                slamHasDestination = false;
                Player.Locomotion.OnCombatLanded();
                SetSlamPhase(SlamPhase.Land, .04f);
                Vector3 impact = character.bounds.center - Vector3.up * (character.bounds.extents.y - .3f);
                HitSlamContacts(impact, impact, true);
            }
        }

        private bool IsSlamCombatCollider(Collider collider)
        {
            return collider.transform.IsChildOf(transform) ||
                collider.GetComponentInParent<EnemyDamageReaction>() != null ||
                collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() != null;
        }

        private bool SlamBlocked(Vector3 origin, Collider target)
        {
            Vector3 toTarget = target.ClosestPoint(origin) - origin;
            if (toTarget.sqrMagnitude < .0001f) return false;
            foreach (var hit in Physics.RaycastAll(origin, toTarget.normalized, toTarget.magnitude,
                ~0, QueryTriggerInteraction.Ignore))
                if (!IsSlamCombatCollider(hit.collider)) return true;
            return false;
        }

        /// <summary>낙하 경로의 공중 적과 착지 범위의 적을 타격한다. 한 번의 내려찍기는 대상당 한 번만 피해를 준다.</summary>
        private void HitSlamContacts(Vector3 from, Vector3 to, bool impact)
        {
            float radius = impact ? config.slamHitRadius : Mathf.Min(.8f, config.slamHitRadius);
            foreach (var other in Physics.OverlapCapsule(from, to, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(transform) || SlamBlocked(to, other)) continue;
                var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                if (enemy != null && enemy.CurrentHealth > 0 && (impact || enemy.IsAirborneForCombo) && slamHits.Add(enemy))
                {
                    if (enemy.IsAirborneForCombo) enemy.ReceiveAirComboHit(transform, 0, true);
                    else enemy.ReceiveAerialHit(transform, 1);
                }
                var boss = other.GetComponentInParent<CombatSystem.Boss.BossCombat>();
                if (impact && boss != null && slamBossHits.Add(boss)) boss.TryReceivePlayerHit();
            }
        }

        /// <summary>피격·사망 등으로 취소되면 내려찍기 타격을 끄고 남은 낙하는 기존 중력에 넘긴다.</summary>
        public void CancelAirSlam()
        {
            if (!IsAirSlamming) return;
            bool grounded = IsSlamLanding;
            slamPhase = SlamPhase.None;
            slamHasDestination = false;
            slamHits.Clear();
            slamBossHits.Clear();
            if (grounded) Active = fastFall = falling = landing = false;
            else { falling = false; Fall(); }
        }
    }
}
