using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using CombatSystem.Combat;
using CombatSystem.Player;
using CombatSystem.Data.Audio;

namespace CombatSystem.Audio
{
    /// <summary>실제 이동·접지 상태에서 애니메이션의 왼발/오른발 접지 시점을 따라 발소리를 재생한다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator)), DefaultExecutionOrder(120)]
    public sealed class CharacterFootstepAudio : MonoBehaviour
    {
        [SerializeField] private FootstepAudioConfig config;
        private Animator animator;
        private PlayerCharacter player;
        private CharacterController character;
        private EnemyStateMachine enemy;
        private NavMeshAgent agent;
        private AudioSource source;
        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>(12);
        private AnimationClip trackedClip;
        private int trackedState, lastLeft = -1, lastRight = -1;
        private Vector3 previousPosition;
        private float previousProgress, lastStepTime = float.NegativeInfinity;

        public int FootstepsPlayedCount { get; private set; }
        public int LeftStepsPlayedCount { get; private set; }
        public int RightStepsPlayedCount { get; private set; }
        public float LastContactProgress { get; private set; }
        public AnimationClip LastGaitClip { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            player = GetComponent<PlayerCharacter>();
            character = GetComponent<CharacterController>();
            enemy = GetComponent<EnemyStateMachine>();
            agent = GetComponent<NavMeshAgent>();
            var child = new GameObject("Stone Footsteps");
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = player != null ? 0f : 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.priority = player != null ? 140 : 200;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            trackedClip = null;
            lastStepTime = float.NegativeInfinity;
        }

        private void LateUpdate()
        {
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            displacement.y = 0f;
            float dt = Time.deltaTime;
            // 벽에 막혀 제자리에서 발만 움직이거나, 순간이동/재배치된 경우 소리를 내지 않는다.
            if (config == null || !animator.isActiveAndEnabled || dt <= 0f ||
                displacement.sqrMagnitude < .01f * dt * dt || displacement.sqrMagnitude > 1600f * dt * dt ||
                !CanPlayFootsteps())
            {
                trackedClip = null;
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            // 전환 중 두 상태의 이벤트를 모두 재생하지 않고 화면에 더 크게 반영되는 상태를 따른다.
            bool useNext = animator.IsInTransition(0) && animator.GetAnimatorTransitionInfo(0).normalizedTime >= .5f;
            if (useNext)
            {
                state = animator.GetNextAnimatorStateInfo(0);
                animator.GetNextAnimatorClipInfo(0, clips);
            }
            else animator.GetCurrentAnimatorClipInfo(0, clips);

            AnimationClip dominant = null;
            float weight = 0f;
            foreach (var info in clips)
                if (info.weight > weight) { dominant = info.clip; weight = info.weight; }
            var gait = config.Find(dominant);
            if (gait == null) { trackedClip = null; return; }

            float progress = state.normalizedTime;
            if (dominant != trackedClip || state.fullPathHash != trackedState || progress < previousProgress ||
                progress - previousProgress > .5f)
            {
                // 새 모션에 진입할 때 이미 지난 발 디딤을 뒤늦게 재생하지 않는다.
                trackedClip = dominant;
                trackedState = state.fullPathHash;
                previousProgress = progress;
                return;
            }
            bool left = Crossed(previousProgress, progress, gait.leftContact);
            bool right = Crossed(previousProgress, progress, gait.rightContact);
            if (left) PlayStep(true, gait, progress);
            if (right) PlayStep(false, gait, progress);
            previousProgress = progress;
        }

        private bool CanPlayFootsteps()
        {
            if (player != null)
            {
                if (!player.isActiveAndEnabled || character == null || !character.enabled || !character.isGrounded ||
                    player.Locomotion == null || (player.WeaponStance != null && player.WeaponStance.IsTransitioning)) return false;
                var state = player.CurrentState;
                return state == PlayerState.Walk || state == PlayerState.Run;
            }
            if (enemy == null || !enemy.isActiveAndEnabled || agent == null || !agent.enabled ||
                !agent.isOnNavMesh || agent.isOnOffMeshLink) return false;
            var current = enemy.CurrentState;
            return current == enemy.PatrolState || current == enemy.ChaseState ||
                current == enemy.ObserveState || current == enemy.RetreatState;
        }

        private static bool Crossed(float from, float to, float contact)
        {
            return Mathf.FloorToInt(to - contact) > Mathf.FloorToInt(from - contact);
        }

        private void PlayStep(bool left, FootstepAudioConfig.Gait gait, float progress)
        {
            // Blend Tree의 우세 클립이 바뀌는 경계에서 같은 디딤이 연달아 울리는 것을 막는다.
            if (Time.time - lastStepTime < .12f) return;
            var clip = left ? Pick(config.leftSteps, ref lastLeft) : Pick(config.rightSteps, ref lastRight);
            if (clip == null) return;
            source.outputAudioMixerGroup = config.outputMixerGroup;
            source.minDistance = Mathf.Max(.1f, config.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + .1f, config.maxDistance);
            float volume = player != null ? (gait.running ? config.playerRunVolume : config.playerWalkVolume) :
                (gait.running ? config.enemyRunVolume : config.enemyWalkVolume);
            source.PlayOneShot(clip, volume);
            lastStepTime = Time.time;
            LastContactProgress = Mathf.Repeat(progress, 1f);
            LastGaitClip = gait.clip;
            FootstepsPlayedCount++;
            if (left) LeftStepsPlayedCount++; else RightStepsPlayedCount++;
        }

        private static AudioClip Pick(AudioClip[] pool, ref int previous)
        {
            if (pool == null || pool.Length == 0) return null;
            int index = Random.Range(0, pool.Length);
            if (pool.Length > 1 && index == previous) index = (index + Random.Range(1, pool.Length)) % pool.Length;
            for (int i = 0; i < pool.Length; i++)
            {
                int candidate = (index + i) % pool.Length;
                if (pool[candidate] == null) continue;
                previous = candidate;
                return pool[candidate];
            }
            return null;
        }

        private void OnDisable() { trackedClip = null; if (source != null) source.Stop(); }
        private void OnDestroy() { if (source != null) Destroy(source.gameObject); }
    }
}
