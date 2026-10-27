using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 입력 시점 대신 실제 공격의 판정 구간에서 휘두르기 소리를 재생한다.
    /// 공격 시퀀스와 타격별 비트를 사용해 연타 입력, 느린 재생, 전환 중 중복 소리를 막는다.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerCharacter)), DefaultExecutionOrder(110)]
    public sealed class PlayerCombatAudio : MonoBehaviour
    {
        [SerializeField] private PlayerCombatAudioConfig config;
        private PlayerCharacter player;
        private Animator animator;
        private PlayerWeaponHitbox weapon;
        private AudioSource swingSource, parrySource;
        private int sequence = -1, playedStrikes;
        private int lastNormal = -1, lastSkill = -1, lastClash = -1;
        private int lastParryFrame = -1;
        private float previousProgress;

        public int SwingPlayedCount { get; private set; }
        public int ParryPlayedCount { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
            weapon = GetComponentInChildren<PlayerWeaponHitbox>(true);
            swingSource = CreateSource("Sword Swishes", 96);
            parrySource = CreateSource("Parry Clashes", 64);
        }

        private AudioSource CreateSource(string objectName, int priority)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.minDistance = 4f;
            source.maxDistance = 30f;
            source.priority = priority;
            return source;
        }

        private void LateUpdate()
        {
            if (config == null || player.Combat == null || animator == null || Time.timeScale <= 0f) return;
            var combat = player.Combat;
            if (sequence != combat.AttackSequence)
            {
                sequence = combat.AttackSequence;
                playedStrikes = 0;
                previousProgress = 0f;
            }
            if (player.IsDead || player.IsHitReacting || player.IsDodging || player.IsGuarding ||
                !combat.IsAttacking || combat.IsChargingPower || combat.IsParryAttempt) return;

            string expected = "Base Layer." + combat.CurrentAttackStateName;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(expected)) state = next;
            }
            if (!state.IsName(expected)) return;
            var hit = weapon != null ? weapon.Config : null;
            if (hit == null) return;
            float progress = combat.SourceAttackProgress(state);

            if (player.AirCombo.IsAirSlamming)
            {
                if (player.AirCombo.IsSlamFalling) PlayWindow(1f, new Vector2(0f, 1f), 0, true);
                return;
            }
            if (player.AirCombo.IsAirDashing)
                PlayWindow(progress, new Vector2(.15f, .7f), 0, true);
            else if (combat.IsCounterAttacking)
                PlayWindow(progress, player.Profile.parry.counterHitWindow, 0, true);
            else if (combat.IsUsingSkill)
            {
                PlayWindow(progress, hit.skillFirstHitWindow, 0, true);
                PlayWindow(progress, hit.skillHitWindow, 1, true);
            }
            else if (combat.IsReleasingPower)
            {
                PlayWindow(progress, player.Profile.powerAttack.dashHitWindow, 0, true);
                PlayWindow(progress, player.Profile.powerAttack.slamHitWindow, 1, true);
            }
            else if (combat.IsUsingPiercingSkill)
                PlayWindow(progress, new Vector2(player.Profile.piercingDash.startProgress, 1f), 0, true);
            else if (combat.IsUsingSkillC)
                PlayWindow(progress, hit.skillCHitWindow, 0, true);
            else if (combat.IsUsingUpperAttack)
                PlayWindow(progress, hit.upperHitWindow, 0, true);
            else if (combat.IsUsingChaseAttack)
                PlayWindow(progress, hit.chaseHitWindow, 0, true);
            else if (combat.IsUsingEFollowUp)
                PlayWindow(progress, player.Profile.combat.eFollowUpHitWindow, 0, true);
            else if (combat.IsUsingAirCombo)
                PlayWindow(progress, GetWindow(hit.airHitWindows, combat.AirComboIndex, hit.hitWindow), 0, false);
            else if (combat.IsUsingGroundCombo)
                PlayWindow(progress, combat.AttackComboIndex == hit.groundKickComboIndex ? hit.groundKickWindow :
                    GetWindow(hit.groundHitWindows, combat.AttackComboIndex, hit.hitWindow), 0, false);

            previousProgress = progress;
        }

        private static Vector2 GetWindow(Vector2[] windows, int index, Vector2 fallback)
        {
            return windows != null && index >= 0 && index < windows.Length ? windows[index] : fallback;
        }

        private void PlayWindow(float progress, Vector2 window, int strike, bool skill)
        {
            int bit = 1 << strike;
            // 이전 프레임도 비교하므로 짧은 판정 구간을 한 프레임에 넘어가도 한 번만 재생한다.
            if ((playedStrikes & bit) != 0 || progress < window.x || previousProgress > window.y) return;
            playedStrikes |= bit;
            var clip = skill ? Pick(config.skillSwishes, ref lastSkill) : Pick(config.normalSwishes, ref lastNormal);
            if (Play(swingSource, clip, skill ? config.skillVolume : config.normalVolume)) SwingPlayedCount++;
        }

        /// <summary>일반 적/보스/투사체 패링의 공통 성공 피드백에서만 호출한다.</summary>
        public void PlayParrySuccess()
        {
            if (!isActiveAndEnabled || config == null || player == null || player.IsDead ||
                Time.timeScale <= 0f || lastParryFrame == Time.frameCount) return;
            lastParryFrame = Time.frameCount;
            if (Play(parrySource, Pick(config.parryClashes, ref lastClash), config.parryVolume)) ParryPlayedCount++;
        }

        private static AudioClip Pick(AudioClip[] clips, ref int previous)
        {
            if (clips == null || clips.Length == 0) return null;
            // 한 종류가 연속되지 않게 하되, 배열에 비어 있는 슬롯이 있어도 다른 클립을 찾는다.
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

        private bool Play(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null || clip == null) return false;
            source.outputAudioMixerGroup = config.outputMixerGroup;
            source.spatialBlend = config.spatialBlend;
            // 패링 슬로 모션에도 타격음의 피치는 그대로 유지한다.
            source.pitch = 1f;
            source.PlayOneShot(clip, volume);
            return true;
        }

        private void OnDisable()
        {
            if (swingSource != null) swingSource.Stop();
            if (parrySource != null) parrySource.Stop();
        }

        private void OnDestroy()
        {
            if (swingSource != null) Destroy(swingSource.gameObject);
            if (parrySource != null) Destroy(parrySource.gameObject);
        }
    }
}
