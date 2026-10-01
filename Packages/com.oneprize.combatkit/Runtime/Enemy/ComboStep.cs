using System;

namespace CombatSystem.Combat
{
    [Serializable]
    public class ComboStep
    {
        public string animStateName = "Combo1";
        public float windupDuration = 0.5f;
        public float parryWindowDuration = 0.25f;
        public float recoveryDuration = 0.35f;
        public bool isStrongAttack;
    }
}
