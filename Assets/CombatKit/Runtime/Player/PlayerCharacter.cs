using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>Actions gated by <see cref="PlayerCharacter.CanStartAction"/>.</summary>
    public enum PlayerAction { Attack, Skill, UpperAttack, Dodge, LinkJump }

    /// <summary>What the player is doing right now, highest priority first.</summary>
    public enum PlayerState { HitReact, Dodge, LinkJump, Attack, AirRecovery, Guard, Jump, Run, Walk, Idle, Dead }

    /// <summary>
    /// The single player component. Owns every player module and drives them in a fixed
    /// order, so features never depend on Unity's per-component Update ordering.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public sealed class PlayerCharacter : MonoBehaviour, ICombatant
    {
        [Tooltip("Basis for camera-relative movement. A Cinemachine camera resolves to the Unity camera it drives. Falls back to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Every tuning value for this character, grouped by module.")]
        [SerializeField] private PlayerProfile profile;

        public Transform Transform => transform;
        public CombatTeam Team => CombatTeam.Player;

        public PlayerContext Context { get; private set; }
        /// <summary>The character's controls: an IPlayerInput on this GameObject, else no input.</summary>
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
        /// The camera the player actually sees, used as the basis for camera-relative input.
        /// A Cinemachine virtual camera assigned here is resolved to the Unity camera its brain
        /// drives: when another virtual camera (e.g. lock-on) is live, the assigned one keeps
        /// its own orientation off-screen and would no longer match what is shown.
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

        public PlayerState CurrentState
        {
            get
            {
                if (IsDead) return PlayerState.Dead;
                if (IsHitReacting) return PlayerState.HitReact;
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

        /// <summary>
        /// The one place that decides whether a new action may start. Busy states block
        /// everything; attacks and skills may still be requested mid-attack so they can buffer.
        /// </summary>
        public bool CanStartAction(PlayerAction action)
        {
            if (IsDead || IsHitReacting || IsGuarding || IsDodging || IsLinkJumping) return false;
            switch (action)
            {
                case PlayerAction.Attack: return true;
                case PlayerAction.Skill: return !IsAirComboActive;
                case PlayerAction.UpperAttack: return !IsAttacking;
                case PlayerAction.Dodge: return !IsAirComboActive && (!IsAttacking || Combat.CanDodgeCancel);
                default: return !IsAttacking && !IsAirComboActive;
            }
        }

        [SerializeField] private ParryFeedback parryFeedback;
        public void PlayParryFeedback()
        {
            if (parryFeedback == null) parryFeedback = GetComponent<ParryFeedback>();
            if (parryFeedback != null) parryFeedback.Play();
        }

        private void Awake()
        {
            var controls = GetComponent<IPlayerInput>();
            // Compare as UnityEngine.Object so Unity's "missing component" null is treated as null.
            if (controls as UnityEngine.Object != null) Controls = controls;
            else Debug.LogWarning("PlayerCharacter: no IPlayerInput component (e.g. PlayerInputReader); the character will ignore input.", this);
            Context = new PlayerContext(this);
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

        private void OnEnable() { CombatRegistry.Register(this); }

        private void OnDisable()
        {
            CombatRegistry.Unregister(this);
            foreach (var module in Modules()) module.OnDisable();
        }

        private void Start()
        {
            Combat.PlayIdle();
        }

        private void Update()
        {
            if (IsDead)
            {
                TickDeath();
                return;
            }
            float dt = Time.deltaTime;
            Health.Tick();
            LockOn.Tick(); // before Combat so a new attack faces the current lock target
            Combat.Tick();
            Locomotion.Tick(dt); // also ticks the active air combo / dodge / link jump
            AirCombo.TickEnemyCollision();
            PiercingDash.Tick();
        }

        private void LateUpdate()
        {
            if (IsDead) return;
        }

        private void OnAnimatorMove()
        {
            if (IsDead) return;
            RootMotion.Apply(Context.Animator.deltaPosition);
        }

        public void EnterDeath()
        {
            Combat.CancelForDamage();
            foreach (var module in Modules()) if (module != Health) module.OnDisable();
            CombatRegistry.Unregister(this);
            var animator = Context.Animator;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(parameter.nameHash, false);
            deathFallSpeed = 0f;
        }

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

        private void OnAnimatorIK(int layerIndex)
        {
            AttackHeightOffset.OnAnimatorIK(layerIndex);
        }
    }
}
