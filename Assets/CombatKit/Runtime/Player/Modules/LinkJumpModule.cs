using UnityEngine;
using Unity.AI.Navigation;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 링크 점프. NavMeshLink 끝점 근처에서 Space를 누르면 가장 가까운 링크를 따라 포물선으로 건너뛴다.
    /// </summary>
    public sealed class LinkJumpModule : PlayerModule
    {
        #region 필드

        public bool IsTraversing { get; private set; }
        private PlayerProfile.LinkJumpSettings config;
        private Vector3 departure, destination;
        private float elapsed, duration, height;
        /// <summary>점프 단계: 0 = 도약(LinkJumpStart), 1 = 비행(LinkJumpLoop), 2 = 착지(LinkJumpEnd).</summary>
        private int phase;

        #endregion

        #region 링크 탐색

        /// <summary>프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.linkJump;
        }

        /// <summary>캡슐 중심 피벗에서 발바닥(지면 접촉점)까지의 높이 차. 링크 끝점(지면)과 피벗을 변환할 때 쓴다.</summary>
        private Vector3 FootOffset => Vector3.up *
            (character.height * 0.5f * transform.lossyScale.y -
             character.center.y * transform.lossyScale.y + character.skinWidth);

        /// <summary>링크의 시작/끝 지점을 월드 좌표로 구한다. 지정된 Transform이 있으면 그 위치를 우선한다.</summary>
        private static Vector3 Endpoint(NavMeshLink link, bool end)
        {
            var target = end ? link.endTransform : link.startTransform;
            return target != null ? target.position :
                link.transform.position + link.transform.rotation * (end ? link.endPoint : link.startPoint);
        }

        /// <summary>
        /// 발 위치에서 활성화 거리 안에 있는 링크 끝점(링크 폭 고려, 양방향이면 양쪽)을 찾아 가장 가까운 것으로 점프를 시작한다.
        /// </summary>
        /// <returns>다른 동작 중이거나 근처에 사용할 수 있는 링크가 없으면 false.</returns>
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
            // 링크 끝점은 지면 위치이고 플레이어 피벗은 캡슐 중심이므로 FootOffset만큼 올려 맞춘다.
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

        #endregion

        #region 점프 진행

        /// <summary>
        /// 도약 → 포물선 비행 → 착지 단계를 진행한다. 피격되거나 비행 중 벽 등에 막혀 궤도를 벗어나면 즉시 종료한다.
        /// </summary>
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

        /// <summary>링크 점프를 끝내고 점프 관련 애니메이터 값을 초기화한다. idle이면 대기 상태로 크로스페이드한다.</summary>
        private void Finish(bool idle)
        {
            IsTraversing = false;
            animator.SetBool("isJumping", false);
            animator.SetInteger("jumpCount", 0);
            if (idle) animator.CrossFadeInFixedTime("Base Layer.Idle", .1f);
        }

        /// <summary>플레이어 비활성화 시 진행 중인 링크 점프를 종료한다.</summary>
        public override void OnDisable()
        {
            if (IsTraversing) Finish(true);
        }

        #endregion
    }
}
