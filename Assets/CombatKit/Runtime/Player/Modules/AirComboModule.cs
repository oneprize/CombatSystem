using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Mid-air hang time while chaining air attacks, the fall and landing that follow,
    /// and the scripted rise of the UpperAttack launcher.
    /// </summary>
    public sealed class AirComboModule : PlayerModule
    {
        public bool Active { get; private set; }
        public bool IsAirDashing { get; private set; }
        private float dashElapsed, dashTravel, dashEndWait;
        private bool dashAttackQueued;
        private CombatSystem.Combat.EnemyDamageReaction dashTarget;
        private readonly HashSet<CombatSystem.Combat.EnemyDamageReaction> dashHits = new HashSet<CombatSystem.Combat.EnemyDamageReaction>();
        private readonly HashSet<CombatSystem.Boss.BossCombat> dashBossHits = new HashSet<CombatSystem.Boss.BossCombat>();

        public bool TryBeginAirDash()
        {
            if (IsAirDashing || !character.enabled || Player.IsHitReacting || Player.IsDodging ||
                Player.IsLinkJumping || Player.IsGuarding || Player.Health.CurrentHealth <= 0 || character.isGrounded)
                return false;
            // Check real floor clearance: locomotion's cached grounded flag is stale during an upper attack.
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

        public void CancelAirDash()
        {
            if (!IsAirDashing) return;
            IsAirDashing = false;
            falling = false;
            Fall();
        }

        private void DashContacts(Vector3 from, Vector3 to)
        {
            float radius = .9f;
            foreach (var other in Physics.OverlapCapsule(from + Vector3.up * .5f, to + Vector3.up * .5f,
                radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(transform)) continue;
                Vector3 point = other.ClosestPoint(to + Vector3.up * .5f);
                Vector3 direction = point - (to + Vector3.up * .5f);
                // An overlap must not hit through arena walls.
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

        private float previousDashProgress;
        private AnimatorStateInfo DashState()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("AirDashAttack"))
                state = animator.GetNextAnimatorStateInfo(0);
            return state;
        }
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
        public bool Recovering => Active && falling;
        private PlayerProfile.AirComboSettings config;
        private bool falling, landing, fastFall;
        public bool AllowsLandingFollowUp => fastFall && Recovering;
        private float verticalSpeed, heldTime;
        private float previousUpperHeight;
        private readonly List<Collider> ignoredEnemyColliders = new List<Collider>();
        private bool ignoringEnemies;
        // Foot-height trajectory sampled from the original UpperAttack at 30 fps.
        // Standalone humanoid clip Bake flags do not reliably survive asset reloads.
        private static readonly float[] UpperHeights = { 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.85341f, 1.11443f, 1.16875f, 1.22002f, 1.26748f, 1.30045f, 1.32557f, 1.38839f, 1.50463f, 1.60975f, 1.66568f, 1.71637f, 1.76262f, 1.80183f, 1.83267f, 1.85791f, 1.88061f, 1.90397f, 1.93004f, 1.83954f, 1.66474f, 1.45540f, 1.23242f, 0.66809f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f };

        protected override void OnInitialize()
        {
            config = Player.Profile.airCombo;
        }

        public void BeginUpperMotion() { previousUpperHeight = 0; }

        /// <summary>
        /// While the player is airborne in combat (UpperAttack, air combo and its fall), the
        /// controller ignores enemy bodies so it can never come to rest on top of one.
        /// Hit detection uses overlap queries, which IgnoreCollision does not affect.
        /// After landing, each pair is restored only once the bodies no longer overlap,
        /// so the player is never popped out of an enemy.
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

        private void RestoreEnemyCollisions()
        {
            foreach (var col in ignoredEnemyColliders)
                if (col != null) Physics.IgnoreCollision(character, col, false);
            ignoredEnemyColliders.Clear();
            ignoringEnemies = false;
        }

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

        // Fraction of the leap completed at every 5% of ChaseUpAttack, sampled from the source clip
        // (Attack_Up_Floor_To_Air_02): crouch and step in until 40%, take off, reach the apex at 90%.
        private static readonly float[] ChaseRise = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, .22f, .39f, .54f, .68f, .80f, .88f, .94f, .97f, .99f, 1f, 1f, 1f };
        private static readonly float[] ChaseTravel = { 0f, .05f, .05f, .10f, .31f, .45f, .59f, .74f, .85f, .88f, .91f, .95f, .96f, .96f, .97f, .97f, .98f, .99f, 1f, 1f, 1f };
        private Vector3 chaseStart, chaseEnd;

        /// <summary>
        /// Plans the chase leap from the ground to the launched enemy: end level with it (feet to
        /// feet) and <paramref name="contactDistance"/> in front of it, or where the player already is if closer.
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

        private static float Sample(float[] curve, float normalizedTime)
        {
            float sample = Mathf.Clamp01(normalizedTime) * (curve.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(sample), curve.Length - 2);
            return Mathf.Lerp(curve[index], curve[index + 1], sample - index);
        }

        public bool CanBegin()
        {
            if (Active || !character.enabled || character.isGrounded) return false;
            var bottom = character.bounds.center - Vector3.up * character.bounds.extents.y;
            return !Physics.Raycast(bottom + Vector3.up * .05f, Vector3.down, .2f, ~0, QueryTriggerInteraction.Ignore);
        }

        public void Begin()
        {
            Active = true; fastFall = falling = landing = false; verticalSpeed = heldTime = 0;
        }

        public void Fall(bool fast = false)
        {
            if (!Active || falling) return;
            falling = true; fastFall = fast; verticalSpeed = fast ? -config.finisherFallSpeed : 0f;
            if (!Player.IsHitReacting) animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop", .1f, 0, 0);
        }

        private bool TryLandingAttack()
        {
            if (!fastFall || Player.IsHitReacting) return false;
            Active = false;
            Player.Locomotion.OnCombatLanded();
            if (Player.Combat.TryLandingFollowUp()) { fastFall = false; return true; }
            Active = true;
            return false;
        }

        public void Tick(float dt)
        {
            if (!Active) return;
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

        public override void OnDisable() { IsAirDashing = false; Active = fastFall = falling = landing = false; RestoreEnemyCollisions(); }
    }
}
