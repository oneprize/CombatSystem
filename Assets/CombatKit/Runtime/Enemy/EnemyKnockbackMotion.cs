using UnityEngine;
using CombatSystem.Data.Enemy;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 넉백·띄우기·공중 콤보 체공·착지 바운스를 처리하는 적 전용 간이 탄도 물리다.
    /// 다운 리액션이 활성인 동안에만 적의 위치 이동을 소유하며, 캡슐 스윕으로 벽과 바닥을 통과하지 않게 막는다.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CapsuleCollider), typeof(Animator))]
    public sealed class EnemyKnockbackMotion : MonoBehaviour
    {
        #region 필드

        [SerializeField] private EnemyKnockbackMotionConfig config;
        /// <summary>넉백 물리가 적 이동을 담당하고 있는지 여부.</summary>
        public bool Active { get; private set; }
        public bool Grounded { get; private set; }
        /// <summary>직전 IK 패스에서 몸통 높이를 바닥에 맞추려고 보정한 양.</summary>
        public float LastPoseCorrection { get; private set; }
        private Vector3 velocity;
        /// <summary>공중 콤보 피격으로 공중에 정지해 있을 남은 시간.</summary>
        private float airHoldRemaining;
        private CapsuleCollider capsule;
        private Animator animator;
        /// <summary>바닥 관통 보정에 사용할 접지 후보 본(엉덩이, 머리, 손발).</summary>
        private Transform[] contactBones;
        /// <summary>충돌면과 남겨 둘 여유 간격.</summary>
        private const float Skin = .015f;

        #endregion

        #region Unity 생명주기

        /// <summary>config가 비어 있으면 기본값으로 대체하고, 휴머노이드라면 포즈 보정용 접지 본을 수집한다.</summary>
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

        #endregion

        #region 발사와 체공 제어

        /// <summary>설정된 수평·수직 속도로 적을 공격자 반대 방향으로 날려 보내고 넉백 물리를 시작한다.</summary>
        public void Launch(Vector3 away)
        {
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            velocity = away.normalized * config.launchSpeed + Vector3.up * config.upwardSpeed;
            Grounded = false;
            airHoldRemaining = 0;
            Active = true;
        }

        /// <summary>올려베기용 발사. 넉백 물리를 시작하되 속도를 상승 위주의 값으로 덮어쓴다.</summary>
        public void LaunchUpper(Vector3 away, float liftSpeed, float pushSpeed)
        {
            Launch(away);
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            velocity = away.normalized * Mathf.Max(0, pushSpeed) + Vector3.up * Mathf.Max(0, liftSpeed);
        }

        /// <summary>
        /// 공중 콤보 피격 반응. 일반 타격은 잠시 공중에 정지시켜 다음 타격을 이어 갈 수 있게 하고, 피니시는 바닥으로 내리꽂는다.
        /// </summary>
        public void AirComboImpact(bool finisher)
        {
            if (!Active || Grounded) return;
            airHoldRemaining = finisher ? 0 : 1.3f;
            velocity = finisher ? Vector3.down * 8f : Vector3.zero;
        }

        /// <summary>지정 높이까지 튀어 오르도록 상승 속도를 주어 착지 바운스를 일으킨다.</summary>
        public void Bounce(float height)
        {
            velocity = Vector3.up * Mathf.Sqrt(2f * config.gravity * Mathf.Max(0f, height));
            airHoldRemaining = 0f;
            Grounded = false;
            Active = true;
        }

        #endregion

        #region 물리 이동

        /// <summary>
        /// 캡슐을 지정 방향으로 스윕해 이동을 막는 가장 가까운 충돌을 찾는다.
        /// 자기 자신, 충돌 무시 설정된 콜라이더, 이동 방향을 막지 않는 면은 제외한다.
        /// </summary>
        /// <returns>막는 충돌이 있으면 true.</returns>
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
                // 플레이어가 이 적과의 충돌을 꺼 둔 몸체(공중 콤보, 관통 돌진 중)도 이동을 막지 않는다.
                if (Physics.GetIgnoreCollision(capsule, hit.collider)) continue;
                if (Vector3.Dot(direction, hit.normal) >= -.001f) continue;
                nearest = hit;
                best = hit.distance;
            }
            return best < float.PositiveInfinity;
        }

        /// <summary>수평 이동을 벽 앞에서 멈추도록 적용하고, 벽에 막히면 수평 속도를 없앤다.</summary>
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

        /// <summary>
        /// 넉백 탄도를 한 프레임 진행한다. 공중 정지 시간을 먼저 소모한 뒤 수평 이동, 중력, 바닥·천장 충돌과 착지를 처리한다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!Active) return;
            if (airHoldRemaining > 0)
            {
                float held = Mathf.Min(deltaTime, airHoldRemaining);
                airHoldRemaining -= held;
                deltaTime -= held;
            }
            // 프레임이 낮아도 바닥이나 벽을 뚫고 지나가지 않도록 작은 단위로 나눠 진행한다.
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

        /// <summary><paramref name="offset"/>만큼 수평으로 밀어 옮기되 벽에서 멈춘다. 실제로 적용된 이동량을 돌려준다.</summary>
        public Vector3 Push(Vector3 offset)
        {
            offset.y = 0;
            Vector3 before = transform.position;
            MoveHorizontal(offset);
            return transform.position - before;
        }

        /// <summary>지상 피격 루트 모션처럼 넉백 물리와 무관한 수평 이동을 벽 충돌을 고려해 적용한다.</summary>
        public void ApplyGroundMotion(Vector3 delta)
        { delta.y = 0; MoveHorizontal(delta); }

        /// <summary>넉백 후 바닥에 착지한 상태에서만 기상 등 회복 동작의 수평 루트 모션을 적용한다.</summary>
        public void ApplyRecoveryMotion(Vector3 delta)
        {
            if (!Active || !Grounded) return;
            delta.y = 0;
            MoveHorizontal(delta);
        }

        #endregion

        #region 포즈 보정과 정지

        /// <summary>
        /// 넉백 중이거나 기상(AerialGetUp) 중일 때, 가장 낮은 접지 본이 캡슐 바닥에 맞도록 몸통 높이를 보정해
        /// 애니메이션이 바닥을 파고들거나 뜨지 않게 한다.
        /// </summary>
        private void OnAnimatorIK(int layerIndex)
        {
            LastPoseCorrection = 0;
            if (layerIndex != 0 || contactBones == null) return;
            bool recovering = animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.AerialGetUp");
            if (animator.IsInTransition(0))
                recovering |= animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.AerialGetUp");
            // Q 피격은 물리 넉백 없이 같은 기상 클립을 쓰므로,
            // 그 진입·종료 블렌드 동안에도 바닥 위로 유지한다.
            if (!Active && !recovering) return;
            float lowest = float.PositiveInfinity;
            foreach (var bone in contactBones)
                if (bone != null) lowest = Mathf.Min(lowest, bone.position.y);
            if (float.IsPositiveInfinity(lowest)) return;
            // 클립에 저작된 몸통 높이 변화는 제거하고, 실제 탄도 궤적은 캡슐이 따라가게 한다.
            LastPoseCorrection = capsule.bounds.min.y + config.groundClearance - lowest;
            if (!Active) LastPoseCorrection = Mathf.Max(0, LastPoseCorrection);
            animator.bodyPosition += Vector3.up * LastPoseCorrection;
        }

        /// <summary>넉백 물리를 중단하고 속도·체공 상태를 초기화해 이동 소유권을 내려놓는다.</summary>
        public void Stop()
        {
            Active = Grounded = false;
            airHoldRemaining = 0;
            velocity = Vector3.zero;
        }
        /// <summary>비활성화될 때 진행 중인 넉백을 정지한다.</summary>
        private void OnDisable() { Stop(); }

        #endregion
    }
}
