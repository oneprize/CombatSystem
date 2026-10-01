using UnityEngine;
using Unity.AI.Navigation;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>Arcing jump across the nearest NavMeshLink, started with Space next to its endpoint.</summary>
    public sealed class LinkJumpModule : PlayerModule
    {
        public bool IsTraversing { get; private set; }
        private PlayerProfile.LinkJumpSettings config;
        private Vector3 departure, destination;
        private float elapsed, duration, height;
        private int phase;

        protected override void OnInitialize()
        {
            config = Player.Profile.linkJump;
        }

        private Vector3 FootOffset => Vector3.up *
            (character.height * 0.5f * transform.lossyScale.y -
             character.center.y * transform.lossyScale.y + character.skinWidth);

        private static Vector3 Endpoint(NavMeshLink link, bool end)
        {
            var target = end ? link.endTransform : link.startTransform;
            return target != null ? target.position :
                link.transform.position + link.transform.rotation * (end ? link.endPoint : link.startPoint);
        }

        public bool TryBegin()
        {
            if (!Player.isActiveAndEnabled || IsTraversing || !character.enabled || !Player.CanStartAction(PlayerAction.LinkJump)) return false;
            Vector3 feet = transform.position - FootOffset;
            float best = config.activationDistance;
            bool found = false;
            Vector3 chosen = default;
            foreach (var link in Object.FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None))
            {
                if (!link.isActiveAndEnabled || !link.activated) continue;
                Vector3 a = Endpoint(link, false), b = Endpoint(link, true);
                for (int side = 0; side < (link.bidirectional ? 2 : 1); side++)
                {
                    Vector3 from = side == 0 ? a : b, to = side == 0 ? b : a;
                    Vector3 along = to - from;
                    along.y = 0f;
                    Vector3 right = Vector3.Cross(Vector3.up, along.normalized);
                    float lateral = Mathf.Clamp(Vector3.Dot(feet - from, right), -link.width * .5f, link.width * .5f);
                    from += right * lateral; to += right * lateral;
                    Vector3 delta = feet - from;
                    if (Mathf.Abs(delta.y) > config.endpointHeightTolerance) continue;
                    delta.y = 0f;
                    if (delta.magnitude > best) continue;
                    best = delta.magnitude; chosen = to; found = true;
                }
            }
            if (!found) return false;
            // Endpoints describe the ground, while the player's pivot is at capsule centre.
            departure = transform.position;
            destination = chosen + FootOffset;
            duration = Mathf.Max(.55f, Vector3.Distance(departure, destination) / config.travelSpeed);
            height = Mathf.Max(config.arcHeight, Mathf.Abs(destination.y - departure.y) * .75f);
            Vector3 facing = destination - departure; facing.y = 0f;
            if (facing.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(facing);
            elapsed = 0f; phase = 0; IsTraversing = true;
            animator.SetBool("isMoving", false); animator.SetBool("isRunning", false);
            animator.SetBool("isTurning", false); animator.SetBool("isJumping", true);
            animator.SetBool("isGrounded", false);
            animator.CrossFadeInFixedTime("Base Layer.LinkJumpStart", .08f, 0, 0f);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsTraversing) return;
            if (Player.IsHitReacting) { Finish(false); return; }
            elapsed += deltaTime;
            if (phase == 0)
            {
                if (elapsed < config.takeoffTime) return;
                elapsed = 0f; phase = 1;
                animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop", .12f, 0, 0f);
            }
            if (phase == 1)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 desired = Vector3.Lerp(departure, destination, t) +
                    Vector3.up * (4f * height * t * (1f - t));
                character.Move(desired - transform.position);
                if (Vector3.Distance(transform.position, desired) > .35f)
                {
                    Finish(true);
                    return;
                }
                if (t < 1f) return;
                phase = 2; elapsed = 0f;
                animator.SetBool("isJumping", false); animator.SetBool("isGrounded", true);
                animator.CrossFadeInFixedTime("Base Layer.LinkJumpEnd", .08f, 0, 0f);
            }
            else if (phase == 2)
            {
                if (animator.IsInTransition(0)) return;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Base Layer.LinkJumpEnd") && state.normalizedTime >= 1f) Finish(true);
            }
        }

        private void Finish(bool idle)
        {
            IsTraversing = false;
            animator.SetBool("isJumping", false);
            animator.SetInteger("jumpCount", 0);
            if (idle) animator.CrossFadeInFixedTime("Base Layer.Idle", .1f);
        }

        public override void OnDisable()
        {
            if (IsTraversing) Finish(true);
        }
    }
}
