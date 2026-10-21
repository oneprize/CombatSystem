using UnityEngine;
using Unity.Cinemachine;
using CombatSystem.Core;
using CombatSystem.Player;

namespace CombatSystem.CameraControl
{
    /// <summary>
    /// 플레이어에게 락온 대상이 있는 동안 락온 전용 Cinemachine 카메라로 전환한다.
    /// 락온 카메라는 <see cref="pivot"/>(플레이어 위치에서 적 방향을 바라보는 기준점)을 어깨 너머로 따라가며,
    /// 플레이어와 적을 함께 담은 타겟 그룹을 조준해 둘 다 화면에 들어오게 한다. 자유 시점 카메라와의 전환은 Cinemachine이 블렌딩한다.
    /// 파워 공격 중에는 두 카메라의 댐핑을 일시적으로 키워 카메라 움직임을 부드럽게 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)] // CinemachineBrain이 카메라를 움직이기 전에 실행
    public sealed class LockOnCamera : MonoBehaviour
    {
        #region 필드

        [Tooltip("The normal orbit camera. Its tracking target is used as the player's camera point.")]
        [SerializeField] private CinemachineCamera freeLookCamera;
        [Tooltip("Higher-priority camera that is enabled only while locked on.")]
        [SerializeField] private CinemachineCamera lockOnCamera;
        [Tooltip("Follow target of the lock-on camera: at the player, facing the enemy.")]
        [SerializeField] private Transform pivot;
        [Tooltip("Look-at target of the lock-on camera.")]
        [SerializeField] private CinemachineTargetGroup targetGroup;
        [Tooltip("Moved onto the locked enemy every frame; member of the target group.")]
        [SerializeField] private Transform enemyAimPoint;
        [Min(0f)] [SerializeField] private float enemyAimHeight = 1.2f;
        [Tooltip("How quickly the camera swings around when the target moves or changes.")]
        [Min(0f)] [SerializeField] private float turnSharpness = 8f;


        [Header("Power Attack Camera")]
        [Tooltip("Higher damping makes camera movement during power attacks slower and smoother.")]
        [SerializeField] private Vector3 powerPositionDamping = new Vector3(1.5f, 1.8f, 1.5f);
        [SerializeField] private Vector2 powerAimDamping = new Vector2(.8f, .8f);
        [Min(.01f)] [SerializeField] private float powerBlendIn = .15f;
        [Min(.01f)] [SerializeField] private float powerBlendOut = .45f;
        [Min(.1f)] [SerializeField] private float powerTurnSharpness = 2.5f;
        private float powerCameraWeight;
        private CameraDamping freeDamping, lockDamping;

        #endregion

        #region 파워 공격 카메라 댐핑

        /// <summary>
        /// Cinemachine 카메라 하나의 위치/조준 댐핑 값을 저장해 두었다가, 가중치에 따라
        /// 원래 값과 파워 공격용 값 사이로 보간해 적용하는 헬퍼.
        /// </summary>
        private sealed class CameraDamping
        {
            private readonly CinemachineOrbitalFollow orbit;
            private readonly CinemachineThirdPersonFollow thirdPerson;
            private readonly CinemachineRotationComposer aim;
            private Vector3 position;
            private Vector2 rotation;

            /// <summary>카메라에 붙은 추적(OrbitalFollow 또는 ThirdPersonFollow)/조준(RotationComposer) 컴포넌트를 찾아 둔다.</summary>
            public CameraDamping(CinemachineCamera camera)
            {
                if (camera == null) return;
                orbit = camera.GetComponent<CinemachineOrbitalFollow>();
                thirdPerson = camera.GetComponent<CinemachineThirdPersonFollow>();
                aim = camera.GetComponent<CinemachineRotationComposer>();
            }

            /// <summary>파워 공격 연출 직전의 원래 댐핑 값을 저장한다.</summary>
            public void Capture()
            {
                if (orbit != null) position = orbit.TrackerSettings.PositionDamping;
                else if (thirdPerson != null) position = thirdPerson.Damping;
                if (aim != null) rotation = aim.Damping;
            }

            /// <summary>가중치(0 = 원래 값, 1 = 파워 공격 값)에 따라 보간한 댐핑을 카메라에 적용한다.</summary>
            public void Apply(float weight, Vector3 targetPosition, Vector2 targetAim)
            {
                // 원래 더 강한 댐핑으로 설정된 카메라를 오히려 빠르게 만들지 않도록 큰 값을 택한다.
                Vector3 p = Vector3.Lerp(position, Vector3.Max(position, targetPosition), weight);
                Vector2 r = Vector2.Lerp(rotation, Vector2.Max(rotation, targetAim), weight);
                if (orbit != null) orbit.TrackerSettings.PositionDamping = p;
                if (thirdPerson != null) thirdPerson.Damping = p;
                if (aim != null) aim.Damping = r;
            }
        }

        /// <summary>
        /// 파워 공격 중이면 카메라 댐핑 가중치를 서서히 올리고, 끝나면 서서히 내린다.
        /// 가중치가 0에서 출발할 때마다 현재 댐핑을 원래 값으로 새로 저장한다.
        /// </summary>
        private void UpdatePowerCamera()
        {
            bool active = player != null && player.isActiveAndEnabled &&
                player.Combat != null && player.Combat.IsUsingPowerAttack;
            if (powerCameraWeight <= 0f)
            {
                if (!active) return;
                freeDamping.Capture();
                lockDamping.Capture();
            }
            float duration = active ? powerBlendIn : powerBlendOut;
            powerCameraWeight = Mathf.MoveTowards(powerCameraWeight, active ? 1f : 0f,
                Time.deltaTime / Mathf.Max(.01f, duration));
            freeDamping.Apply(powerCameraWeight, powerPositionDamping, powerAimDamping);
            lockDamping.Apply(powerCameraWeight, powerPositionDamping, powerAimDamping);
        }

        /// <summary>파워 공격 연출 중에 꺼지면 카메라 댐핑을 원래 값으로 즉시 되돌린다.</summary>
        private void OnDisable()
        {
            if (powerCameraWeight <= 0f) return;
            freeDamping?.Apply(0f, powerPositionDamping, powerAimDamping);
            lockDamping?.Apply(0f, powerPositionDamping, powerAimDamping);
            powerCameraWeight = 0f;
        }

        #endregion

        #region 락온 카메라 전환

        private PlayerCharacter player;
        private Transform groupPlayerPoint;

        /// <summary>두 카메라의 댐핑 헬퍼를 만들고, 시작 시에는 락온 카메라를 꺼 둔다.</summary>
        private void Awake()
        {
            freeDamping = new CameraDamping(freeLookCamera);
            lockDamping = new CameraDamping(lockOnCamera);
            if (lockOnCamera != null) lockOnCamera.enabled = false;
        }

        /// <summary>
        /// 락온 대상 유무에 따라 락온 카메라를 켜고 끈다. 락온 중에는 적 조준점을 적 위로 옮기고,
        /// 피벗을 플레이어 위치에서 적 방향으로 회전시킨다(처음 락온한 프레임은 즉시, 이후에는 부드럽게).
        /// </summary>
        private void LateUpdate()
        {
            if (player == null) player = FindPlayer();
            UpdatePowerCamera();
            if (freeLookCamera == null || lockOnCamera == null || pivot == null || targetGroup == null || enemyAimPoint == null) return;
            var target = player != null ? player.LockOn.Target : null;
            Transform playerPoint = freeLookCamera.Target.TrackingTarget;
            bool locked = target != null && playerPoint != null;

            bool justLocked = locked && !lockOnCamera.enabled;
            if (lockOnCamera.enabled != locked) lockOnCamera.enabled = locked;
            if (!locked) return;

            // 플레이어 카메라 기준점이 바뀌었을 때만 타겟 그룹 구성을 다시 만든다.
            if (groupPlayerPoint != playerPoint)
            {
                groupPlayerPoint = playerPoint;
                targetGroup.Targets.Clear();
                targetGroup.AddMember(playerPoint, 1f, .6f);
                targetGroup.AddMember(enemyAimPoint, 1f, .8f);
            }

            enemyAimPoint.position = target.Transform.position + Vector3.up * enemyAimHeight;
            pivot.position = playerPoint.position;
            Vector3 toEnemy = enemyAimPoint.position - playerPoint.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < .0001f) return;
            var facing = Quaternion.LookRotation(toEnemy, Vector3.up);
            pivot.rotation = justLocked ? facing
                : Quaternion.Slerp(pivot.rotation, facing, 1f - Mathf.Exp(-Mathf.Lerp(turnSharpness, Mathf.Min(turnSharpness, powerTurnSharpness), powerCameraWeight) * Time.deltaTime));
        }

        /// <summary>전투 레지스트리에 등록된 플레이어 팀 전투원 중 PlayerCharacter를 찾는다.</summary>
        private static PlayerCharacter FindPlayer()
        {
            foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Player))
                if (combatant is PlayerCharacter character) return character;
            return null;
        }

        #endregion
    }
}
