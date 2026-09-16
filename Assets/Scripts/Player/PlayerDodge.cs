using UnityEngine;
namespace ParrySystem.Player
{
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class PlayerDodge : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float crossFadeTime = 0.06f;
        [SerializeField, Min(0f)] private float distanceMultiplier = 1f;
        public bool IsDodging { get; private set; }
        public string CurrentStateName { get; private set; }
        private Animator animator;
        private CharacterController character;
        private PlayerCombat combat;
        private Vector3 direction;
        private void Awake()
        {
            animator = GetComponent<Animator>();
            character = GetComponent<CharacterController>();
            combat = GetComponent<PlayerCombat>();
        }
        public bool TryBegin(Vector3 worldInput)
        {
            if (!isActiveAndEnabled || IsDodging || !character.enabled ||
                (combat != null && (combat.isAttacking || combat.IsGuarding || combat.IsHitReacting || combat.IsLinkJumping))) return false;
            worldInput.y = 0f;
            direction = worldInput.sqrMagnitude > .001f ? worldInput.normalized : transform.forward;
            CurrentStateName = "Base Layer.DodgeFront";
            if (!animator.HasState(0, Animator.StringToHash(CurrentStateName))) return false;
            // Face the movement direction so the forward dodge matches its displacement.
            direction.y = 0f;
            direction.Normalize();
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
            IsDodging = true;
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isTurning", false);
            animator.SetBool("isJumping", false);
            animator.CrossFadeInFixedTime(CurrentStateName, crossFadeTime, 0, 0f);
            return true;
        }
        public void Tick(float dt)
        {
            if (!IsDodging) return;
            if (combat != null && combat.IsHitReacting) { IsDodging = false; return; }
            character.Move(Vector3.down * 2f * dt);
            if (animator.IsInTransition(0)) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(CurrentStateName) && state.normalizedTime < 1f) return;
            IsDodging = false;
            animator.CrossFadeInFixedTime("Base Layer.Idle", .1f);
        }
        public void ApplyRootMotion(Vector3 delta)
        {
            if (!IsDodging || !character.enabled) return;
            delta.y = 0f;
            character.Move(direction * (delta.magnitude * distanceMultiplier));
        }
        private void OnDisable() { IsDodging = false; }
    }
}
