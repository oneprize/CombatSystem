using UnityEngine;
using Unity.Cinemachine;
using CombatSystem.Core;
using CombatSystem.Player;

namespace CombatSystem.CameraControl
{
    /// <summary>
    /// Switches to the lock-on Cinemachine camera while the player has a lock-on target.
    /// The lock-on camera follows <see cref="pivot"/> (placed at the player, turned toward the
    /// enemy) from over the shoulder and aims at a target group holding the player and the
    /// enemy, so both stay in frame. Cinemachine blends between it and the free-look camera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)] // before the CinemachineBrain moves the camera
    public sealed class LockOnCamera : MonoBehaviour
    {
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

        private sealed class CameraDamping
        {
            private readonly CinemachineOrbitalFollow orbit;
            private readonly CinemachineThirdPersonFollow thirdPerson;
            private readonly CinemachineRotationComposer aim;
            private Vector3 position;
            private Vector2 rotation;

            public CameraDamping(CinemachineCamera camera)
            {
                if (camera == null) return;
                orbit = camera.GetComponent<CinemachineOrbitalFollow>();
                thirdPerson = camera.GetComponent<CinemachineThirdPersonFollow>();
                aim = camera.GetComponent<CinemachineRotationComposer>();
            }

            public void Capture()
            {
                if (orbit != null) position = orbit.TrackerSettings.PositionDamping;
                else if (thirdPerson != null) position = thirdPerson.Damping;
                if (aim != null) rotation = aim.Damping;
            }

            public void Apply(float weight, Vector3 targetPosition, Vector2 targetAim)
            {
                // Never speed up a camera that was already configured with stronger damping.
                Vector3 p = Vector3.Lerp(position, Vector3.Max(position, targetPosition), weight);
                Vector2 r = Vector2.Lerp(rotation, Vector2.Max(rotation, targetAim), weight);
                if (orbit != null) orbit.TrackerSettings.PositionDamping = p;
                if (thirdPerson != null) thirdPerson.Damping = p;
                if (aim != null) aim.Damping = r;
            }
        }

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

        private void OnDisable()
        {
            if (powerCameraWeight <= 0f) return;
            freeDamping?.Apply(0f, powerPositionDamping, powerAimDamping);
            lockDamping?.Apply(0f, powerPositionDamping, powerAimDamping);
            powerCameraWeight = 0f;
        }

        private PlayerCharacter player;
        private Transform groupPlayerPoint;

        private void Awake()
        {
            freeDamping = new CameraDamping(freeLookCamera);
            lockDamping = new CameraDamping(lockOnCamera);
            if (lockOnCamera != null) lockOnCamera.enabled = false;
        }

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

        private static PlayerCharacter FindPlayer()
        {
            foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Player))
                if (combatant is PlayerCharacter character) return character;
            return null;
        }
    }
}
