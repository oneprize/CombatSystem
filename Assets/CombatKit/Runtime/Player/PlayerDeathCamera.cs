using UnityEngine;
using Unity.Cinemachine;

namespace CombatSystem.Player
{
    /// <summary>
    /// 플레이어가 죽으면 최우선순위 Cinemachine 카메라를 생성해, 현재 시점에서 쓰러지는 몸을 비추는
    /// 측면 시점으로 부드럽게 이동/회전/FOV 전환하는 사망 연출 카메라.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class PlayerDeathCamera : MonoBehaviour
    {
        #region 필드

        [SerializeField] private CinemachineCamera cameraPrefab;
        [SerializeField, Min(.1f)] private float duration = 2.2f;
        [SerializeField] private Vector3 endOffset = new Vector3(2.4f, 1.7f, 2.6f);
        [SerializeField, Range(20f, 90f)] private float endFieldOfView = 48f;
        private PlayerCharacter player;
        private Animator animator;
        private Transform hips;
        private CinemachineCamera deathCamera;
        private Vector3 startPosition;
        private Quaternion startRotation, facing;
        private float startFov, elapsed;
        /// <summary>사망 카메라 연출이 진행 중인지 여부.</summary>
        public bool IsPlaying => deathCamera != null;

        #endregion

        #region Unity 생명주기

        /// <summary>플레이어/애니메이터 참조를 캐시하고, 쓰러지는 몸을 따라가기 위해 골반 본을 찾는다.</summary>
        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman) hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        }

        /// <summary>
        /// 사망 직후 메인 카메라 시점에서 사망 카메라를 생성하고, 이후 매 프레임 목표 시점으로 보간한다.
        /// 몸과 카메라 사이에 벽이 있으면 카메라를 앞으로 당겨 가려지지 않게 한다.
        /// </summary>
        private void LateUpdate()
        {
            if (player == null || !player.IsDead) return;
            if (deathCamera == null)
            {
                if (cameraPrefab == null || Camera.main == null) return;
                var view = Camera.main;
                startPosition = view.transform.position;
                startRotation = view.transform.rotation;
                startFov = view.fieldOfView;
                facing = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                deathCamera = Instantiate(cameraPrefab, startPosition, startRotation);
                deathCamera.name = "Player Death Camera";
                deathCamera.Lens.FieldOfView = startFov;
                deathCamera.Priority = 10000;
            }
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            // 서 있는 캡슐의 중심이 아니라 실제로 쓰러지는 몸(골반)을 따라간다.
            Vector3 focus = hips != null ? hips.position : transform.position + Vector3.up * .6f;
            focus += Vector3.up * .15f;
            Vector3 end = new Vector3(focus.x, transform.position.y, focus.z) + facing * endOffset;
            Vector3 desired = Vector3.Lerp(startPosition, end, t);
            Vector3 ray = desired - focus;
            float distance = ray.magnitude;
            if (distance > .01f)
            {
                foreach (var hit in Physics.SphereCastAll(focus, .15f, ray / distance, distance,
                    ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) ||
                        hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>() != null ||
                        hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() != null) continue;
                    distance = Mathf.Min(distance, Mathf.Max(.2f, hit.distance - .1f));
                }
                desired = focus + ray.normalized * distance;
            }
            deathCamera.transform.position = desired;
            Vector3 look = focus - desired;
            if (look.sqrMagnitude > .001f)
                deathCamera.transform.rotation = Quaternion.Slerp(startRotation, Quaternion.LookRotation(look), t);
            deathCamera.Lens.FieldOfView = Mathf.Lerp(startFov, endFieldOfView, t);
        }

        /// <summary>생성한 사망 카메라를 제거하고 연출 진행 시간을 초기화한다.</summary>
        private void OnDisable()
        {
            if (deathCamera != null) Destroy(deathCamera.gameObject);
            deathCamera = null;
            elapsed = 0f;
        }

        #endregion
    }
}
