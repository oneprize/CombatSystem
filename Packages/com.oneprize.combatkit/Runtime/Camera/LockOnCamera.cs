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

        private PlayerCharacter player;
        private Transform groupPlayerPoint;

        private void Awake()
        {
            if (lockOnCamera != null) lockOnCamera.enabled = false;
        }

        private void LateUpdate()
        {
            if (freeLookCamera == null || lockOnCamera == null || pivot == null || targetGroup == null || enemyAimPoint == null) return;
            if (player == null) player = FindPlayer();
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
                : Quaternion.Slerp(pivot.rotation, facing, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
        }

        private static PlayerCharacter FindPlayer()
        {
            foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Player))
                if (combatant is PlayerCharacter character) return character;
            return null;
        }
    }
}
