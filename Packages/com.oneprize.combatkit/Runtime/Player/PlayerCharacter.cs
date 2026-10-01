using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>Actions gated by <see cref="PlayerCharacter.CanStartAction"/>.</summary>
    public enum PlayerAction { Attack, Skill, UpperAttack, Dodge, LinkJump }

    /// <summary>What the player is doing right now, highest priority first.</summary>
    public enum PlayerState { HitReact, Dodge, LinkJump, Attack, AirRecovery, Guard, Jump, Run, Walk, Idle }

    /// <summary>
    /// The single player component. Owns every player module and drives them in a fixed
    /// order, so features never depend on Unity's per-component Update ordering.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public sealed class PlayerCharacter : MonoBehaviour, ICombatant
    {
        [Tooltip("Basis for camera-relative movement. Falls back to Camera.main.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Slow-motion/zoom played on a successful parry. Optional; may live on the camera.")]
        [SerializeField] private ParryFeedback parryFeedback;

        [Tooltip("Every tuning value for this character, grouped by module.")]
        [SerializeField] private PlayerProfile profile;

        public Transform Transform => transform;
        public CombatTeam Team => CombatTeam.Player;

        public PlayerContext Context { get; private set; }
        public HealthModule Health { get; private set; }
        public CombatModule Combat { get; private set; }
        public LocomotionModule Locomotion { get; private set; }
        public DodgeModule Dodge { get; private set; }
        public LinkJumpModule LinkJump { get; private set; }
        public AirComboModule AirCombo { get; private set; }
        public PiercingDashModule PiercingDash { get; private set; }
        public GatherSkillModule GatherSkill { get; private set; }
        public AttackHeightOffsetModule AttackHeightOffset { get; private set; }
        public RootMotionModule RootMotion { get; private set; }

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

        public Transform CameraTransform
        {
            get
            {
                if (cameraTransform != null) return cameraTransform;
                var main = Camera.main;
                return main != null ? main.transform : transform;
            }
        }

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
            if (IsHitReacting || IsGuarding || IsDodging || IsLinkJumping) return false;
            switch (action)
            {
                case PlayerAction.Attack: return true;
                case PlayerAction.Skill: return !IsAirComboActive;
                case PlayerAction.UpperAttack: return !IsAttacking;
                default: return !IsAttacking && !IsAirComboActive;
            }
        }

        public void PlayParryFeedback()
        {
            if (parryFeedback == null) parryFeedback = GetComponent<ParryFeedback>();
            if (parryFeedback != null) parryFeedback.Play();
        }

        private void Awake()
        {
            Context = new PlayerContext(this);
            Health = new HealthModule();
            Combat = new CombatModule();
            Locomotion = new LocomotionModule();
            Dodge = new DodgeModule();
            LinkJump = new LinkJumpModule();
            AirCombo = new AirComboModule();
            PiercingDash = new PiercingDashModule();
            GatherSkill = new GatherSkillModule();
            AttackHeightOffset = new AttackHeightOffsetModule();
            RootMotion = new RootMotionModule();
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
            yield return GatherSkill;
            yield return AttackHeightOffset;
            yield return RootMotion;
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
            float dt = Time.deltaTime;
            Health.Tick();
            Combat.Tick();
            Locomotion.Tick(dt); // also ticks the active air combo / dodge / link jump
            PiercingDash.Tick();
        }

        private void LateUpdate()
        {
            GatherSkill.LateTick();
        }

        private void OnAnimatorMove()
        {
            RootMotion.Apply(Context.Animator.deltaPosition);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            AttackHeightOffset.OnAnimatorIK(layerIndex);
        }
    }
}
