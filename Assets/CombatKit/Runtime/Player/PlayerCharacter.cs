using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary><see cref="PlayerCharacter.CanStartAction"/>으로 시작 가능 여부를 판정하는 플레이어 행동 종류.</summary>
    public enum PlayerAction { Attack, Skill, UpperAttack, Dodge, LinkJump }

    /// <summary>플레이어의 현재 상태. 여러 상태가 겹치면 앞에 선언된 것이 우선한다.</summary>
    public enum PlayerState { HitReact, Dodge, LinkJump, Attack, AirRecovery, Guard, Jump, Run, Walk, Idle, Dead, Hanging, WallRun, SlopeSlide, HighFallRoll }

    /// <summary>
    /// 플레이어의 유일한 컴포넌트. 모든 플레이어 모듈을 소유하고 고정된 순서로 구동하므로,
    /// 기능들이 Unity의 컴포넌트별 Update 실행 순서에 의존하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public sealed class PlayerCharacter : MonoBehaviour, ICombatant
    {
        #region 필드

        [Tooltip("Basis for camera-relative movement. A Cinemachine camera resolves to the Unity camera it drives. Falls back to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Every tuning value for this character, grouped by module.")]
        [SerializeField] private PlayerProfile profile;

        #endregion

        #region 프로퍼티

        public Transform Transform => transform;
        public CombatTeam Team => CombatTeam.Player;

        public PlayerContext Context { get; private set; }
        public PlayerHang Hang { get; private set; }
        public PlayerLedgeHang LedgeHang { get; private set; }
        public PlayerWallRun WallRun { get; private set; }
        public PlayerSlopeSlide SlopeSlide { get; private set; }
        public PlayerHighFallRoll HighFallRoll { get; private set; }
        public PlayerWeaponStance WeaponStance { get; private set; }
        /// <summary>캐릭터 조작 입력. 같은 GameObject의 IPlayerInput을 쓰고, 없으면 아무 입력도 없는 NullPlayerInput을 쓴다.</summary>
        public IPlayerInput Controls { get; private set; } = NullPlayerInput.Instance;
        public HealthModule Health { get; private set; }
        public CombatModule Combat { get; private set; }
        public LocomotionModule Locomotion { get; private set; }
        public DodgeModule Dodge { get; private set; }
        public LinkJumpModule LinkJump { get; private set; }
        public AirComboModule AirCombo { get; private set; }
        public PiercingDashModule PiercingDash { get; private set; }
        public AttackHeightOffsetModule AttackHeightOffset { get; private set; }
        public RootMotionModule RootMotion { get; private set; }
        public LockOnModule LockOn { get; private set; }
        /// <summary>캐릭터 튜닝 값 묶음. 인스펙터에서 할당되지 않았으면 경고를 남기고 기본값 인스턴스를 만들어 쓴다.</summary>
        public PlayerProfile Profile
        {
            get
            {
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<PlayerProfile>();
                    Debug.LogWarning("PlayerCharacter: no PlayerProfile assigned, using built-in defaults.", this);
                }
                return profile;
            }
        }

        /// <summary>
        /// 플레이어가 실제로 보고 있는 카메라로, 카메라 기준 이동 입력의 기준축이 된다.
        /// Cinemachine 가상 카메라가 할당되면 그 Brain이 구동하는 Unity 카메라로 변환한다.
        /// 다른 가상 카메라(예: 락온)가 활성일 때 할당된 카메라는 화면 밖에서 자기 방향을 유지하므로 실제 화면과 어긋나기 때문이다.
        /// </summary>
        public Transform CameraTransform
        {
            get
            {
                if (cameraTransform != null)
                {
                    var virtualCamera = cameraTransform.GetComponent<Unity.Cinemachine.CinemachineVirtualCameraBase>();
                    if (virtualCamera == null) return cameraTransform;
                    var brain = Unity.Cinemachine.CinemachineCore.FindPotentialTargetBrain(virtualCamera);
                    if (brain != null) return brain.transform;
                }
                var main = Camera.main;
                return main != null ? main.transform : transform;
            }
        }

        public bool IsDead => Health != null && Health.IsDead;
        private float deathFallSpeed;
        private bool restarting;

        public bool IsAttacking => Combat.IsAttacking;
        public bool IsGuarding => Combat.IsGuarding;
        public bool IsHitReacting => Health.IsReacting;
        public bool IsDodging => Dodge.IsDodging;
        public bool IsLinkJumping => LinkJump.IsTraversing;
        public bool IsAirComboActive => AirCombo.Active;

        /// <summary>현재 상태를 우선순위(사망 > 피격 리액션 > 회피 > ... > 대기) 순으로 판정해 하나로 요약한다.</summary>
        public PlayerState CurrentState
        {
            get
            {
                if (IsDead) return PlayerState.Dead;
                if (IsHitReacting) return PlayerState.HitReact;
                if (LedgeHang != null && LedgeHang.IsHanging) return PlayerState.Hanging;
                if (Hang != null && Hang.IsHanging) return PlayerState.Hanging;
                if (WallRun != null && WallRun.IsWallRunning) return PlayerState.WallRun;
                if (SlopeSlide != null && SlopeSlide.IsSliding) return PlayerState.SlopeSlide;
                if (HighFallRoll != null && HighFallRoll.IsRolling) return PlayerState.HighFallRoll;
                if (IsDodging) return PlayerState.Dodge;
                if (IsLinkJumping) return PlayerState.LinkJump;
                if (IsAttacking) return PlayerState.Attack;
                if (IsAirComboActive) return PlayerState.AirRecovery;
                if (IsGuarding) return PlayerState.Guard;
                if (Locomotion.IsJumping) return PlayerState.Jump;
                if (Locomotion.IsRunning) return PlayerState.Run;
                if (Locomotion.IsMoving) return PlayerState.Walk;
                return PlayerState.Idle;
            }
        }

        #endregion

        #region 행동 허용 판정과 패리 피드백

        /// <summary>
        /// 새 행동을 시작해도 되는지 판정하는 유일한 지점. 바쁜 상태(사망/피격/가드/회피/링크 점프)는 모두 막고,
        /// 공격과 스킬은 선입력 버퍼링을 위해 공격 도중에도 요청을 허용한다.
        /// </summary>
        public bool CanStartAction(PlayerAction action)
        {
            if ((LedgeHang != null && LedgeHang.IsHanging) || (HighFallRoll != null && HighFallRoll.IsRolling) || (WallRun != null && WallRun.IsWallRunning) || (WeaponStance != null && WeaponStance.IsTransitioning) || (Hang != null && Hang.IsHanging) || IsDead || IsHitReacting || IsGuarding || IsDodging || IsLinkJumping) return false;
            switch (action)
            {
                case PlayerAction.Attack: return true;
                case PlayerAction.Skill: return !IsAirComboActive;
                case PlayerAction.UpperAttack: return !IsAttacking || Combat.IsUsingGroundCombo;
                case PlayerAction.Dodge: return !IsAirComboActive && (!IsAttacking || Combat.CanDodgeCancel);
                default: return !IsAttacking && !IsAirComboActive;
            }
        }

        [SerializeField] private ParryFeedback parryFeedback;
        /// <summary>패리 성공 연출(슬로 모션 + 카메라 줌인)을 재생한다. 참조가 비어 있으면 같은 GameObject에서 찾아 쓴다.</summary>
        public void PlayParryFeedback()
        {
            var combatAudio = GetComponent<PlayerCombatAudio>();
            if (combatAudio != null) combatAudio.PlayParrySuccess();
            if (parryFeedback == null) parryFeedback = GetComponent<ParryFeedback>();
            if (parryFeedback != null) parryFeedback.Play();
        }

        /// <summary>저스트 회피 연출(슬로 모션 + 카메라 줌아웃)을 재생한다. 참조가 비어 있으면 같은 GameObject에서 찾아 쓴다.</summary>
        public void PlayPerfectDodgeFeedback()
        {
            if (parryFeedback == null) parryFeedback = GetComponent<ParryFeedback>();
            if (parryFeedback != null && parryFeedback.PlayPerfectDodge())
                Debug.Log("[Perfect Dodge] Attack evaded at the last moment", this);
        }

        #endregion

        #region Unity 생명주기

        /// <summary>입력 소스를 찾고 컨텍스트와 모든 모듈을 생성해 바인딩한다.</summary>
        private void Awake()
        {
            var controls = GetComponent<IPlayerInput>();
            // UnityEngine.Object로 비교해야 Unity의 "missing component" 가짜 null도 null로 취급된다.
            if (controls as UnityEngine.Object != null) Controls = controls;
            else Debug.LogWarning("PlayerCharacter: no IPlayerInput component (e.g. PlayerInputReader); the character will ignore input.", this);
            Context = new PlayerContext(this);
            Hang = GetComponent<PlayerHang>();
            LedgeHang = GetComponent<PlayerLedgeHang>();
            WallRun = GetComponent<PlayerWallRun>();
            SlopeSlide = GetComponent<PlayerSlopeSlide>();
            HighFallRoll = GetComponent<PlayerHighFallRoll>();
            WeaponStance = GetComponent<PlayerWeaponStance>();
            Health = new HealthModule();
            Combat = new CombatModule();
            Locomotion = new LocomotionModule();
            Dodge = new DodgeModule();
            LinkJump = new LinkJumpModule();
            AirCombo = new AirComboModule();
            PiercingDash = new PiercingDashModule();
            AttackHeightOffset = new AttackHeightOffsetModule();
            RootMotion = new RootMotionModule();
            LockOn = new LockOnModule();
            foreach (var module in Modules()) module.Bind(Context);
        }

        /// <summary>바인딩/비활성화 등 일괄 처리에 쓰는 전체 모듈 목록을 고정 순서로 열거한다.</summary>
        private IEnumerable<PlayerModule> Modules()
        {
            yield return Health;
            yield return Combat;
            yield return Locomotion;
            yield return Dodge;
            yield return LinkJump;
            yield return AirCombo;
            yield return PiercingDash;
            yield return AttackHeightOffset;
            yield return RootMotion;
            yield return LockOn;
        }

        /// <summary>적이 타겟으로 찾을 수 있도록 전투 레지스트리에 등록한다.</summary>
        private void OnEnable() { CombatRegistry.Register(this); }

        /// <summary>전투 레지스트리에서 빠지고 각 모듈이 진행 중인 작업을 정리하게 한다.</summary>
        private void OnDisable()
        {
            CombatRegistry.Unregister(this);
            foreach (var module in Modules()) module.OnDisable();
        }

        /// <summary>시작 시 대기 애니메이션을 재생한다.</summary>
        private void Start()
        {
            Combat.PlayIdle();
        }

        /// <summary>매 프레임 모듈들을 정해진 순서로 Tick한다. 사망 상태에서는 낙하와 재시작 입력만 처리한다.</summary>
        private void Update()
        {
            if (IsDead)
            {
                TickDeath();
                return;
            }
            float dt = Time.deltaTime;
            Health.Tick();
            if (LedgeHang != null) LedgeHang.CaptureInput();
            if (HighFallRoll != null && HighFallRoll.Tick(dt)) return;
            if (WeaponStance != null && WeaponStance.Tick(dt)) return;
            if (LedgeHang != null && LedgeHang.Tick(dt)) return;
            if (Hang != null && Hang.Tick(dt)) return;
            if (WallRun != null && WallRun.Tick(dt)) return;
            if (SlopeSlide != null && SlopeSlide.Tick(dt)) return;
            LockOn.Tick(); // 새 공격이 현재 락온 대상을 바라보도록 Combat보다 먼저 갱신
            Combat.Tick();
            Locomotion.Tick(dt); // 진행 중인 공중 콤보 / 회피 / 링크 점프도 여기서 함께 Tick됨
            AirCombo.TickEnemyCollision();
            PiercingDash.Tick();
        }

        /// <summary>현재는 사망 시 조기 반환만 하는 빈 후처리 훅이다.</summary>
        private void LateUpdate()
        {
            if (IsDead) return;
        }

        /// <summary>애니메이터의 루트 모션 이동량을 RootMotionModule에 넘겨 실제 이동으로 반영한다.</summary>
        private void OnAnimatorMove()
        {
            if (IsDead) return;
            if (LedgeHang != null && LedgeHang.IsHanging) return;
            if (Hang != null && Hang.IsHanging) return;
            if (WeaponStance != null && WeaponStance.IsTransitioning) return;
            if (WallRun != null && WallRun.IsWallRunning) return;
            if (SlopeSlide != null && SlopeSlide.IsSliding) return;
            if (HighFallRoll != null && HighFallRoll.IsRolling) { HighFallRoll.ApplyRootMotion(Context.Animator.deltaPosition); return; }
            RootMotion.Apply(Context.Animator.deltaPosition);
        }

        #endregion

        #region 사망 처리

        /// <summary>
        /// 사망 상태로 전환한다. 진행 중인 공격을 취소하고 체력 외 모듈을 정리한 뒤,
        /// 레지스트리에서 빠지고 애니메이터 bool 파라미터를 모두 꺼서 다른 상태로 새지 않게 한다.
        /// </summary>
        public void EnterDeath()
        {
            if (LedgeHang != null) LedgeHang.Release(false);
            if (Hang != null) Hang.Release(false);
            if (WallRun != null) WallRun.End(false);
            if (SlopeSlide != null) SlopeSlide.End(false);
            if (HighFallRoll != null) HighFallRoll.Cancel();
            Combat.CancelForDamage();
            foreach (var module in Modules()) if (module != Health) module.OnDisable();
            CombatRegistry.Unregister(this);
            var animator = Context.Animator;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(parameter.nameHash, false);
            deathFallSpeed = 0f;
        }

        /// <summary>사망 중 매 프레임 호출. 시체가 바닥까지 떨어지게 중력을 적용하고, R 키로 현재 씬을 다시 불러온다.</summary>
        private void TickDeath()
        {
            var controller = GetComponent<CharacterController>();
            if (controller.enabled)
            {
                deathFallSpeed = controller.isGrounded ? -2f : deathFallSpeed + Physics.gravity.y * Time.deltaTime;
                controller.Move(Vector3.up * deathFallSpeed * Time.deltaTime);
            }
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (!restarting && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                restarting = true;
                var scene = gameObject.scene;
                Time.timeScale = 1f;
#if UNITY_EDITOR
                if (scene.buildIndex < 0)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                        new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                    return;
                }
#endif
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            }
        }

        #endregion

        #region 애니메이션 IK

        /// <summary>공격 높이 보정 모듈이 IK로 상체/무기 높이를 맞출 수 있도록 IK 패스를 전달한다.</summary>
        private void OnAnimatorIK(int layerIndex)
        {
            // While hanging, align the body to the ledge instead of applying the grounded visual lowering.
            if (LedgeHang != null && LedgeHang.IsHanging) { LedgeHang.ApplyIK(layerIndex); return; }
            Vector3 bodyBefore=Context.Animator.isHuman?Context.Animator.bodyPosition:Vector3.zero;
            AttackHeightOffset.OnAnimatorIK(layerIndex);
            if(SlopeSlide!=null&&Context.Animator.isHuman)
                SlopeSlide.ApplyGroundPose(layerIndex,Context.Animator.bodyPosition-bodyBefore);
            if(Hang!=null)Hang.ApplyHandIK(layerIndex);
            if(LedgeHang!=null)LedgeHang.ApplyIK(layerIndex);
        }

        #endregion
    }
}
