using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Combat;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Skill A / Skill B dash: moves straight through enemies (collisions ignored while
    /// dashing) and hits each one it passes exactly once.
    /// </summary>
    public sealed class PiercingDashModule : PlayerModule
    {
        private PlayerProfile.PiercingDashSettings config;
        private int sequence = -1;
        private bool started, finished;
        private float elapsed;
        private Vector3 direction;
        private readonly List<Collider> ignored = new List<Collider>();
        private readonly HashSet<EnemyDamageReaction> struck = new HashSet<EnemyDamageReaction>();
        private readonly HashSet<CombatSystem.Boss.BossCombat> bosses = new HashSet<CombatSystem.Boss.BossCombat>();
        public bool IsDashing => started && !finished;

        protected override void OnInitialize()
        {
            config = Player.Profile.piercingDash;
        }

        public void Tick()
        {
            if (!Player.Combat.IsUsingPiercingSkill) { RestoreCollisions(); started = false; finished = false; }
        }

        /// <summary>Called from root motion while a piercing skill plays; drives the dash itself.</summary>
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
            // Sweep the traveled segment, including low-FPS movement, once per enemy.
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

        private void RestoreCollisions()
        {
            // Clear overlap before restoring collision if damage/cancel stopped inside an enemy.
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

        public override void OnDisable() { RestoreCollisions(); started = false; finished = false; }
    }
}
