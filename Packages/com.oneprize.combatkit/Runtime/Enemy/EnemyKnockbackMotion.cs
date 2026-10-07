using UnityEngine;
using CombatSystem.Data.Enemy;

namespace CombatSystem.Combat
{
    // Owns displacement only while a knockdown reaction is active.
    [DisallowMultipleComponent, RequireComponent(typeof(CapsuleCollider), typeof(Animator))]
    public sealed class EnemyKnockbackMotion : MonoBehaviour
    {
        [SerializeField] private EnemyKnockbackMotionConfig config;
        public bool Active { get; private set; }
        public bool Grounded { get; private set; }
        public float LastPoseCorrection { get; private set; }
        private Vector3 velocity;
        private float airHoldRemaining;
        private CapsuleCollider capsule;
        private Animator animator;
        private Transform[] contactBones;
        private const float Skin = .015f;

        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EnemyKnockbackMotionConfig>();
                Debug.LogWarning("EnemyKnockbackMotion: no EnemyKnockbackMotionConfig assigned, using built-in defaults.", this);
            }

            capsule = GetComponent<CapsuleCollider>();
            animator = GetComponent<Animator>();
            if (animator.isHuman)
            {
                var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.Head,
                    HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                    HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
                    HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
                contactBones = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++) contactBones[i] = animator.GetBoneTransform(bones[i]);
            }
        }

        public void Launch(Vector3 away)
        {
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            velocity = away.normalized * config.launchSpeed + Vector3.up * config.upwardSpeed;
            Grounded = false;
            airHoldRemaining = 0;
            Active = true;
        }

        public void LaunchUpper(Vector3 away, float liftSpeed, float pushSpeed)
        {
            Launch(away);
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            velocity = away.normalized * Mathf.Max(0, pushSpeed) + Vector3.up * Mathf.Max(0, liftSpeed);
        }

        public void AirComboImpact(bool finisher)
        {
            if (!Active || Grounded) return;
            airHoldRemaining = finisher ? 0 : 1.3f;
            velocity = finisher ? Vector3.down * 8f : Vector3.zero;
        }

        public void Bounce(float height)
        {
            velocity = Vector3.up * Mathf.Sqrt(2f * config.gravity * Mathf.Max(0f, height));
            airHoldRemaining = 0f;
            Grounded = false;
            Active = true;
        }

        private bool Sweep(Vector3 direction, float distance, out RaycastHit nearest)
        {
            var bounds = capsule.bounds;
            float radius = Mathf.Max(.01f, Mathf.Min(bounds.extents.x, bounds.extents.z) - Skin);
            float half = Mathf.Max(0, bounds.extents.y - radius);
            nearest = new RaycastHit();
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.CapsuleCastAll(bounds.center + Vector3.up * half,
                bounds.center - Vector3.up * half, radius, direction, distance,
                config.collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
                // Bodies the player has switched off against this enemy (air combo, piercing dash) do not block it either.
                if (Physics.GetIgnoreCollision(capsule, hit.collider)) continue;
                if (Vector3.Dot(direction, hit.normal) >= -.001f) continue;
                nearest = hit;
                best = hit.distance;
            }
            return best < float.PositiveInfinity;
        }

        private void MoveHorizontal(Vector3 movement)
        {
            if (movement.sqrMagnitude < .000001f) return;
            RaycastHit hit;
            float distance = movement.magnitude;
            if (Sweep(movement / distance, distance + Skin, out hit))
            {
                distance = Mathf.Max(0, hit.distance - Skin);
                velocity.x = velocity.z = 0;
            }
            transform.position += movement.normalized * distance;
            Physics.SyncTransforms();
        }

        public void Tick(float deltaTime)
        {
            if (!Active) return;
            if (airHoldRemaining > 0)
            {
                float held = Mathf.Min(deltaTime, airHoldRemaining);
                airHoldRemaining -= held;
                deltaTime -= held;
            }
            // Small steps keep low frame rates from tunnelling through floors or walls.
            float remaining = deltaTime;
            while (remaining > .00001f)
            {
                float dt = Mathf.Min(.02f, remaining);
                remaining -= dt;
                MoveHorizontal(new Vector3(velocity.x, 0, velocity.z) * dt);
                velocity.y -= config.gravity * dt;
                float dy = velocity.y * dt;
                RaycastHit hit;
                if (Sweep(dy > 0 ? Vector3.up : Vector3.down, Mathf.Abs(dy) + Skin + .005f, out hit))
                {
                    transform.position += Vector3.up * (Mathf.Sign(dy) * Mathf.Max(0, hit.distance - Skin));
                    if (dy <= 0 && hit.normal.y >= .5f)
                    {
                        Grounded = true;
                        velocity = Vector3.zero;
                    }
                    else velocity.y = Mathf.Min(0, velocity.y);
                }
                else
                {
                    transform.position += Vector3.up * dy;
                    Grounded = false;
                }
                Physics.SyncTransforms();
            }
        }

        /// <summary>Moves horizontally by <paramref name="offset"/>, stopping at walls. Returns the displacement actually applied.</summary>
        public Vector3 Push(Vector3 offset)
        {
            offset.y = 0;
            Vector3 before = transform.position;
            MoveHorizontal(offset);
            return transform.position - before;
        }

        public void ApplyGroundMotion(Vector3 delta)
        { delta.y = 0; MoveHorizontal(delta); }

        public void ApplyRecoveryMotion(Vector3 delta)
        {
            if (!Active || !Grounded) return;
            delta.y = 0;
            MoveHorizontal(delta);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            LastPoseCorrection = 0;
            if (layerIndex != 0 || contactBones == null) return;
            bool recovering = animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.AerialGetUp");
            if (animator.IsInTransition(0))
                recovering |= animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.AerialGetUp");
            // Q uses the same get-up clip without launching the physical knockback.
            // Keep its entry and exit blends above the floor as well.
            if (!Active && !recovering) return;
            float lowest = float.PositiveInfinity;
            foreach (var bone in contactBones)
                if (bone != null) lowest = Mathf.Min(lowest, bone.position.y);
            if (float.IsPositiveInfinity(lowest)) return;
            // Remove authored body-height drift; the capsule follows the actual ballistic arc.
            LastPoseCorrection = capsule.bounds.min.y + config.groundClearance - lowest;
            if (!Active) LastPoseCorrection = Mathf.Max(0, LastPoseCorrection);
            animator.bodyPosition += Vector3.up * LastPoseCorrection;
        }

        public void Stop()
        {
            Active = Grounded = false;
            airHoldRemaining = 0;
            velocity = Vector3.zero;
        }
        private void OnDisable() { Stop(); }
    }
}
