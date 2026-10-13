using UnityEngine;
using Unity.Cinemachine;

namespace CombatSystem.Player
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class PlayerDeathCamera : MonoBehaviour
    {
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
        public bool IsPlaying => deathCamera != null;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman) hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        }

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
            // Follow the collapsing body rather than the standing capsule's centre.
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

        private void OnDisable()
        {
            if (deathCamera != null) Destroy(deathCamera.gameObject);
            deathCamera = null;
            elapsed = 0f;
        }
    }
}
