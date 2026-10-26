using UnityEngine;
using CombatSystem.Data.Enemy;

namespace CombatSystem.Combat
{
    /// <summary>적 공격의 실제 히트 구간 시작에 Swishes를 한 번 재생한다.</summary>
    // 무기 접촉으로 패링/그로기 전이가 일어나기 전에 휘두르기 소리를 먼저 재생한다.
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyCombat)), DefaultExecutionOrder(-10)]
    public sealed class EnemyCombatAudio : MonoBehaviour
    {
        [SerializeField] private EnemyCombatAudioConfig config;
        private EnemyStateMachine machine;
        private EnemyCombat combat;
        private EnemyWeaponHitbox weapon;
        private AudioSource source;
        private int sequence = -1, combo = -1, lastWeak = -1, lastStrong = -1;
        private float previousProgress;
        private bool played;

        public int SwingPlayedCount { get; private set; }

        private void Awake()
        {
            machine = GetComponent<EnemyStateMachine>();
            combat = GetComponent<EnemyCombat>();
            weapon = GetComponentInChildren<EnemyWeaponHitbox>(true);
            var child = new GameObject("Enemy Sword Swishes");
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.priority = 160;
        }

        private void LateUpdate()
        {
            if (config == null || machine == null || !machine.isActiveAndEnabled ||
                machine.CurrentState != machine.AttackState || Time.timeScale <= 0f) return;

            var attack = machine.AttackState;
            AnimatorStateInfo state;
            Vector2 window;
            int step;
            bool strong;
            if (attack.IsOpeningAttack)
            {
                if (!attack.TryGetOpeningProgress(out state)) return;
                step = -2;
                window = combat.OpeningHitWindow;
                strong = true;
            }
            else
            {
                if (!attack.TryGetAttackProgress(out state, out step)) return;
                var hit = weapon != null ? weapon.Config : null;
                if (hit == null) return;
                window = step == 0 ? hit.combo1HitWindow : step == 1 ? hit.combo2HitWindow :
                    step == 2 ? hit.combo3HitWindow : hit.combo4HitWindow;
                strong = combat.GetComboStep(step).isStrongAttack;
            }

            if (sequence != attack.AttackSequence || combo != step)
            {
                sequence = attack.AttackSequence;
                combo = step;
                previousProgress = 0f;
                played = false;
            }
            float progress = state.normalizedTime;
            // 프레임이 짧은 타격 구간 전체를 건너뛰더라도 한 번만 재생한다.
            if (!played && progress >= window.x && previousProgress <= window.y)
            {
                played = true;
                var clip = strong ? Pick(config.strongSwishes, ref lastStrong) : Pick(config.weakSwishes, ref lastWeak);
                if (clip != null)
                {
                    source.outputAudioMixerGroup = config.outputMixerGroup;
                    source.minDistance = Mathf.Max(.1f, config.minDistance);
                    source.maxDistance = Mathf.Max(source.minDistance + .1f, config.maxDistance);
                    source.PlayOneShot(clip, strong ? config.strongVolume : config.weakVolume);
                    SwingPlayedCount++;
                }
            }
            previousProgress = progress;
        }

        private static AudioClip Pick(AudioClip[] clips, ref int previous)
        {
            if (clips == null || clips.Length == 0) return null;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == previous) index = (index + Random.Range(1, clips.Length)) % clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                int candidate = (index + i) % clips.Length;
                if (clips[candidate] == null) continue;
                previous = candidate;
                return clips[candidate];
            }
            return null;
        }

        private void OnDisable() { if (source != null) source.Stop(); }
        private void OnDestroy() { if (source != null) Destroy(source.gameObject); }
    }
}
