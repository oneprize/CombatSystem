using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// The power attack gauge: filled by landing hits, drained while charging the power attack.
    /// Measured in stages (1.0 = one stage, up to <see cref="Stages"/>).
    /// </summary>
    public sealed class PowerGaugeModule : PlayerModule
    {
        private PlayerProfile.PowerAttackSettings config;

        public int Stages => Mathf.Max(1, config.stages);
        public float Value { get; private set; }
        /// <summary>Full stages currently stored.</summary>
        public int FilledStages => Mathf.FloorToInt(Value + .0001f);

        protected override void OnInitialize()
        {
            config = Player.Profile.powerAttack;
        }

        /// <summary>A normal attack (or a skill) hit one enemy.</summary>
        public void RegisterHit(bool skill)
        {
            if (Player.Combat.IsUsingPowerAttack) return;
            Value = Mathf.Min(Stages, Value + (skill ? config.gainPerSkillHit : config.gainPerHit));
        }

        /// <summary>Drains up to <paramref name="amount"/> and returns how much was actually removed.</summary>
        public float Drain(float amount)
        {
            float drained = Mathf.Min(Value, Mathf.Max(0f, amount));
            Value -= drained;
            return drained;
        }
    }
}
