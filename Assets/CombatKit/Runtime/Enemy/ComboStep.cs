using System;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 적 콤보 한 타의 애니메이션 상태명과 예비 동작·유효 타격·후딜 구간 길이를 정의하는 데이터다.
    /// EnemyAttackState가 이 값으로 콤보 단계별 타이밍을 진행한다.
    /// </summary>
    [Serializable]
    public class ComboStep
    {
        public string animStateName = "Combo1";
        /// <summary>공격 판정이 시작되기 전 예비 동작 시간(초).</summary>
        public float windupDuration = 0.5f;
        /// <summary>공격 판정이 유효한 시간(초). 예전 이름은 parryWindowDuration이다.</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("parryWindowDuration")] public float activeDuration = 0.25f;
        /// <summary>판정 종료 후 다음 단계로 넘어가기 전까지의 후딜 시간(초).</summary>
        public float recoveryDuration = 0.35f;
        /// <summary>강공격 여부. EnemyCombat.IsCurrentAttackStrong을 통해 외부에서 조회된다.</summary>
        public bool isStrongAttack;
    }
}
