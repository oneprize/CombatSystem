using System;
using UnityEngine;

namespace CombatSystem.Data.Player
{
    /// <summary>
    /// 플레이어 캐릭터 하나의 모든 튜닝 값을 모듈별로 묶은 설정 에셋.
    /// 캐릭터마다 에셋 하나를 만들어 PlayerCharacter에 할당한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CombatKit/Player/Player Profile", fileName = "PlayerProfile")]
    public class PlayerProfile : ScriptableObject
    {
        public ParrySettings parry = new ParrySettings();
        [Serializable]
        public class ParrySettings
        {
            [Min(.05f)] public float inputWindow = .24f;
            [Range(0,180)] public float facingHalfAngle = 80f;
            public Vector2 counterHitWindow = new Vector2(.27f,.40f);
            [Range(.4f,1f)] public float followUpProgress = .55f;
        }
        public CombatSettings combat = new CombatSettings();
        public LocomotionSettings locomotion = new LocomotionSettings();
        public HealthSettings health = new HealthSettings();
        public DodgeSettings dodge = new DodgeSettings();
        public LinkJumpSettings linkJump = new LinkJumpSettings();
        public AirComboSettings airCombo = new AirComboSettings();
        public PiercingDashSettings piercingDash = new PiercingDashSettings();
        public GatherSkillSettings gatherSkill = new GatherSkillSettings();
        public AttackHeightOffsetSettings attackHeightOffset = new AttackHeightOffsetSettings();
        public LockOnSettings lockOn = new LockOnSettings();
        public PowerAttackSettings powerAttack = new PowerAttackSettings();

        /// <summary>공격 버튼을 길게 눌러 모았다가 놓는 파워 공격(차지 → 돌진 베기 → 내려찍기) 설정.</summary>
        [Serializable]
        public class PowerAttackSettings
        {
            /// <summary>공격 버튼을 이 시간(초) 이상 누르고 있으면 일반 공격 대신 파워 공격 차지로 판정한다.</summary>
            [Header("Charge")]
            [Min(.01f)] public float holdThreshold = .3f;
            [Tooltip("Total button hold time at which damage stops increasing. Holding longer keeps charging.")]
            [Min(.31f)] public float maxChargeDuration = 5f;
            [Tooltip("Seconds after charge entry before incoming hits stop interrupting.")]
            [Min(0f)] public float armorAfterSeconds = .7f;
            /// <summary>최소 차지로 해제했을 때의 피해 배율.</summary>
            [Header("Release")]
            [Min(1f)] public float minDamageMultiplier = 2f;
            /// <summary>maxChargeDuration까지 차지했을 때의 피해 배율.</summary>
            [Min(1f)] public float maxDamageMultiplier = 5f;
            [Tooltip("Normalized time of the dash slashes in PowerAttackEnd.")]
            public Vector2 dashHitWindow = new Vector2(.03f, .36f);
            [Min(.1f)] public float dashHitRadius = 1.2f;
            [Tooltip("Normalized time of the closing slam in PowerAttackEnd.")]
            public Vector2 slamHitWindow = new Vector2(.42f, .50f);
            [Min(.1f)] public float slamHitRadius = 3f;
        }

        /// <summary>락온 대상 획득/유지 거리 설정.</summary>
        [Serializable]
        public class LockOnSettings
        {
            [Tooltip("Enemies within this distance can be locked on to.")]
            [Min(0f)] public float acquireRange = 20f;
            [Tooltip("The lock breaks (or moves to the nearest other enemy) beyond this distance.")]
            [Min(0f)] public float keepRange = 25f;
        }

        /// <summary>지상/공중 콤보, 스킬 연계, 가드, 납도 콤보 등 공격 흐름 전반의 타이밍 설정.</summary>
        [Serializable]
        public class CombatSettings
        {
            /// <summary>공격 애니메이터 상태 이름의 접두어(콤보 단계 번호를 붙여 실제 상태명을 만든다).</summary>
            [Header("Attack")]
            public string attackAnimStateName = "Attack";
            [Tooltip("Fallback chain point for steps without an entry in comboChainProgress.")]
            [Range(0f, 1f)] public float comboTransitionProgress = 0.5f;
            [Tooltip("Normalized time per ground combo step at which a queued attack chains into the next step. " +
                     "Set to where the clip's pose matches the next clip's first frame (before the recovery).")]
            public float[] comboChainProgress = { .25f, .30f, .30f };
            [Tooltip("Blend time (seconds) from one ground combo step into the next.")]
            [Min(0f)] public float comboCrossFadeTime = .1f;
            [Tooltip("Blend duration from a finished ground attack back to Idle.")]
            [Min(0f)] public float groundAttackIdleBlendTime = .35f;
            [Header("Attack to dodge")]
            [Tooltip("Normalized time per ground combo step after which a dodge may cancel the attack (end of the swing).")]
            public float[] dodgeCancelProgress = { .25f, .30f, .30f, .40f };
            /// <summary>공격 시작 시 자동으로 바라볼 적을 찾는 최대 거리.</summary>
            [Header("Attack Facing")]
            [Min(0f)] public float attackTargetDistance = 10f;
            /// <summary>자동 조준 대상으로 인정하는 플레이어와의 최대 높이 차이.</summary>
            [Min(0f)] public float attackTargetHeightTolerance = 2f;
            /// <summary>어퍼 공격 중 선입력된 공격이 공중 콤보로 이어지는 정규화 시간.</summary>
            [Header("Air combo")]
            [Range(0, 1)] public float upperAirChainProgress = .22f;
            [Tooltip("Fallback chain point for air steps without an entry in airComboChainProgress.")]
            [Range(0, 1)] public float airChainProgress = .95f;
            [Tooltip("Normalized time per air combo step at which a queued attack chains into the next step. " +
                     "Set to where the clip's pose matches the next clip's first frame (before the recovery).")]
            public float[] airComboChainProgress = { .40f, .33f, .40f };
            [Tooltip("Horizontal gap kept to an airborne attack target; root motion closer than this pushes the target along.")]
            [Min(0f)] public float airAttackContactDistance = 1.1f;
            [Header("Skill C chase (launch -> leap -> air combo)")]
            [Tooltip("Skill C progress from which a buffered attack leaps after the launched enemy (just after the launch hit).")]
            [Range(0, 1)] public float skillCChaseProgress = .45f;
            [Tooltip("Chase leap progress from which a queued attack chains into air combo step 1.")]
            [Range(0, 1)] public float chaseAirChainProgress = .75f;
            /// <summary>스킬 사용 중 입력한 공격을 기억해 두는 선입력 유지 시간(초).</summary>
            [Header("Skill to normal attack")]
            [Min(0f)] public float skillAttackInputBuffer = .4f;
            /// <summary>스킬 G 이후 선입력된 일반 공격으로 넘어갈 수 있는 정규화 시간.</summary>
            [Range(0f, 1f)] public float skillGFollowUpProgress = .75f;
            /// <summary>스킬 A 이후 선입력된 일반 공격으로 넘어갈 수 있는 정규화 시간.</summary>
            [Range(0f, 1f)] public float skillAFollowUpProgress = .65f;
            /// <summary>스킬 B 이후 선입력된 일반 공격으로 넘어갈 수 있는 정규화 시간.</summary>
            [Range(0f, 1f)] public float skillBFollowUpProgress = .65f;
            [Min(0f)] public float skillFollowUpBlendTime = .08f;
            /// <summary>보스 패리 판정 시 직전 공격 입력을 유효로 인정하는 시간(초, unscaled).</summary>
            [Header("Boss parry")]
            [Min(0f)] public float bossParryInputWindow = .15f;
            /// <summary>납도 콤보에서 다음 베기로 이어질 수 있는 정규화 시간.</summary>
            [Header("RMB sheathed combo")]
            [Range(.1f, 1f)] public float sheathedChainProgress = .4f;
            [Min(0f)] public float sheathedBlendTime = .03f;
            /// <summary>납도 콤보 베기의 히트 구간(정규화 시간 시작/끝). 검기 발사 시점도 이 구간 시작을 따른다.</summary>
            public Vector2 sheathedHitWindow = new Vector2(.12f, .35f);
            /// <summary>E 스킬 명중 후 후속 공격 입력을 받아 주는 시간(초).</summary>
            [Header("E hit follow-up")]
            [Min(.05f)] public float eFollowUpInputWindow = .8f;
            /// <summary>E 후속 공격 중 선입력된 공격이 다음 콤보로 이어지는 정규화 시간.</summary>
            [Range(0f, 1f)] public float eFollowUpComboProgress = .6f;
            /// <summary>E 후속 공격의 히트 구간(정규화 시간 시작/끝).</summary>
            public Vector2 eFollowUpHitWindow = new Vector2(.18f, .42f);
            [Header("Normal attack to skill")]
            [Tooltip("Normalized ground attack progress at which E/R may interrupt the combo.")]
            [Range(0f, 1f)] public float piercingSkillTransitionProgress = .4f;
            [Min(0f)] public float piercingSkillBlendTime = .05f;
            /// <summary>일반 공격 중 입력한 스킬(E/R 외)을 기억해 두는 선입력 유지 시간(초).</summary>
            [Min(0f)] public float normalSkillInputBuffer = .4f;
            /// <summary>일반 공격 중 선입력된 스킬(E/R 외)이 콤보를 끊고 시작될 수 있는 정규화 시간.</summary>
            [Range(0f, 1f)] public float normalSkillTransitionProgress = .6f;
            [Min(0f)] public float normalSkillBlendTime = .08f;
            [Header("Guard (우클릭을 누르고 있는 동안 유지)")]
            public string guardAnimStateName = "Guard";
            [Header("Animation")]
            public string idleAnimStateName = "Idle";
            public float crossFadeTime = 0.1f;
            /// <summary>스킬 시전 시 자동으로 바라볼 적을 찾는 최대 거리.</summary>
            [Header("Skill targeting")]
            [Min(0f)] public float skillTargetDistance = 10f;
        }

        /// <summary>걷기/달리기/점프/낙하 등 기본 이동 설정.</summary>
        [Serializable]
        public class LocomotionSettings
        {
            public float walkSpeed = 3f;
            public float runSpeed = 6f;
            public float rotationSpeed = 720f;
            public float jumpForce = 5f;
            public float gravity = -9.81f;
            public float groundCheckDistance = 0.2f; // 바닥 체크용 거리 (캡슐 바닥 기준)
            [Tooltip("Seconds airborne without jumping (walking off a ledge) before the fall animation plays and the first jump counts as used.")]
            [Min(0f)] public float fallStartDelay = 0.15f;
            /// <summary>이동 방향을 반대로 뒤집었을 때 방향 전환 상태를 유지하는 시간(초).</summary>
            public float turningHoldTime = 0.2f;
            [Header("Run turn")]
            [Tooltip("Minimum angle (degrees) between facing and move input while running that plays the 180° run turn.")]
            [Range(90f, 180f)] public float runTurnMinAngle = 135f;
            [Tooltip("Seconds after running stops during which a reversed input still triggers the run turn (covers key-switch gaps and opposite keys held together).")]
            [Min(0f)] public float runTurnInputGrace = .15f;
            [Tooltip("Cross-fade time from Run into the run turn.")]
            [Min(0f)] public float runTurnBlendIn = .08f;
            [Tooltip("Normalized run turn time at which normal locomotion resumes.")]
            [Range(.5f, 1f)] public float runTurnExitProgress = .85f;
            [Tooltip("Cross-fade time from the run turn back into Run/Walk/Idle.")]
            [Min(0f)] public float runTurnBlendOut = .15f;
        }

        /// <summary>플레이어 이름, 최대 체력, 적의 약공격/강공격에 받는 피해량 설정.</summary>
        [Serializable]
        public class HealthSettings
        {
            public string playerName = "Player";
            [Min(1)] public float maxHealth = 100f;
            [Min(0)] public float weakAttackDamage = 10f;
            [Min(0)] public float strongAttackDamage = 20f;
        }

        /// <summary>회피 이동 거리와 회피 → 공격 캔슬 타이밍 설정.</summary>
        [Serializable]
        public class DodgeSettings
        {
            [Min(0f)] public float crossFadeTime = 0.06f;
            /// <summary>회피 애니메이션의 루트 모션 이동량에 곱하는 배율.</summary>
            [Min(0f)] public float distanceMultiplier = 1f;
            [Header("Dodge to attack")]
            [Tooltip("Normalized dodge time at which an attack pressed during the dodge starts the ground combo.")]
            [Range(0f, 1f)] public float attackCancelProgress = .7f;
            [Min(0f)] public float attackBlendTime = .15f;
            [Header("Perfect dodge")]
            [Tooltip("Seconds after the dodge starts during which an evaded hit counts as a perfect dodge (slow motion + zoom out).")]
            [Min(0f)] public float perfectDodgeWindow = .2f;
        }

        /// <summary>링크 지점 사이를 포물선으로 뛰어넘는 링크 점프 설정.</summary>
        [Serializable]
        public class LinkJumpSettings
        {
            /// <summary>링크 점프를 발동할 수 있는 링크 지점까지의 최대 거리.</summary>
            [Min(0.1f)] public float activationDistance = 1.2f;
            /// <summary>링크 지점으로 인정하는 플레이어와의 최대 높이 차이.</summary>
            [Min(0.1f)] public float endpointHeightTolerance = 1f;
            [Min(0.1f)] public float travelSpeed = 5f;
            /// <summary>점프 궤적 포물선의 최고점 높이.</summary>
            [Min(0f)] public float arcHeight = 1.6f;
            /// <summary>도약 동작 후 실제 이동을 시작하기까지 기다리는 시간(초).</summary>
            [Min(0f)] public float takeoffTime = 0.18f;
        }

        /// <summary>공중 콤보 체공/낙하, 공중 E 돌진과 후속타, 공중 E 내려찍기 설정.</summary>
        [Serializable]
        public class AirComboSettings
        {
            [Header("Air E dash and follow-up")]
            [Min(0f)] public float airDashMaxDistance = 2.5f;
            /// <summary>공중 돌진 공격의 루트 모션 이동량에 곱하는 배율.</summary>
            [Range(0f, 1f)] public float airDashMotionScale = .38f;
            /// <summary>공중 돌진 공격 중 선입력된 공격이 후속타로 이어지는 정규화 시간.</summary>
            [Range(0f, 1f)] public float airDashFollowUpProgress = .7f;
            /// <summary>공중 돌진이 끝난 뒤 후속 입력을 기다려 주는 유예 시간(초).</summary>
            [Min(0f)] public float airDashFollowUpGrace = .2f;
            [Min(.1f)] public float fallGravity = 18f;
            /// <summary>공중 콤보 체공을 유지할 수 있는 최대 시간(초). 넘으면 공중 콤보를 강제로 끝낸다.</summary>
            [Min(.1f)] public float maximumHoldTime = 10f;
            /// <summary>공중 콤보 마무리 타격 후 낙하 시작 속도.</summary>
            [Min(1f)] public float finisherFallSpeed = 12f;
            /// <summary>공중 콤보 마무리 타격 후 낙하에 적용하는 중력.</summary>
            [Min(1f)] public float finisherFallGravity = 40f;
            /// <summary>착지 직후 공격 입력을 지상 공격으로 이어 주는 선입력 유지 시간(초).</summary>
            [Min(.01f)] public float landingAttackBuffer = .3f;
            [Header("Air E slam")]
            [Min(.1f)] public float slamFallSpeed = 18f;
            [Min(.1f)] public float slamGravity = 45f;
            [Min(.1f)] public float slamHitRadius = 2.5f;
            /// <summary>내려찍기 착지 후 경직 시간(초). 현재 런타임 코드에서는 참조하지 않는다.</summary>
            [Min(.05f)] public float slamLandingRecovery = .35f;
        }

        /// <summary>관통 돌진 스킬(E)과 스킬 B(R)의 돌진 거리/시간/시작 시점 설정.</summary>
        [Serializable]
        public class PiercingDashSettings
        {
            [Min(0)] public float distance = 5f;
            [Min(.01f)] public float duration = .22f;
            /// <summary>스킬 애니메이션의 이 정규화 시간부터 실제 돌진 이동을 시작한다.</summary>
            [Range(0, 1)] public float startProgress = 15f / 45f;
            [Min(.05f)] public float hitRadius = .7f;
            [Header("Skill B (R)")]
            [Min(0)] public float skillBDistance = 5f;
            [Min(.01f)] public float skillBDuration = .28f;
            [Range(0, 1)] public float skillBStartProgress = .32f;
        }

        /// <summary>
        /// 전방 부채꼴 범위의 적을 플레이어 앞으로 끌어모으는 스킬 설정.
        /// 현재 런타임 코드에서는 참조하지 않으며, 기존 에셋 직렬화 호환을 위해 남아 있다.
        /// </summary>
        [Serializable]
        public class GatherSkillSettings
        {
            [Range(0, 1)] public float recallProgress = .36f;
            [Min(0)] public float range = 5f;
            [Range(0, 90)] public float halfAngle = 30f;
            [Min(.05f)] public float pullDuration = .35f;
            [Min(.5f)] public float gatherDistance = 1.5f;
            [Min(.1f)] public float spacing = .9f;
            [Min(0)] public float heightTolerance = 2f;
        }

        /// <summary>
        /// 애니메이션 클립의 높이 기준이 캐릭터와 달라 생기는 어긋남을 상태별로 보정하는 높이 오프셋 설정.
        /// </summary>
        [Serializable]
        public class AttackHeightOffsetSettings
        {
            [Tooltip("Combo1 원본의 발 기준과 플레이어 몸 중심 기준 사이 높이 차이(m)")]
            public float heightOffset = 0.95f;
            /// <summary>Combo2 클립에 적용할 높이 보정(m). 의미는 heightOffset과 같다.</summary>
            public float combo2HeightOffset = 0.95f;
            /// <summary>Combo3 클립에 적용할 높이 보정(m). 의미는 heightOffset과 같다.</summary>
            public float combo3HeightOffset = 0.19f;
            [Tooltip("플레이어 외형 전체를 아래로 내리는 높이(m)")]
            [Min(0f)] public float visualLowering = 0.12f;
            /// <summary>Guard 상태 클립에 적용할 높이 보정(m).</summary>
            [Min(0f)] public float guardHeightOffset = 0.23f;
            /// <summary>GuardAccept(가드 성공 반응) 상태 클립에 적용할 높이 보정(m).</summary>
            [Min(0f)] public float guardAcceptHeightOffset = 0.25f;
        }
    }
}
