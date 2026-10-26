using UnityEngine;
using UnityEngine.Audio;

namespace CombatSystem.Data.Player
{
    /// <summary>플레이어의 휘두르기와 패링 성공 효과음. CombatKit 안의 오디오만 참조한다.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Player/Combat Audio Config", fileName = "PlayerCombatAudioConfig")]
    public sealed class PlayerCombatAudioConfig : ScriptableObject
    {
        [Header("Swishes")]
        public AudioClip[] normalSwishes;
        public AudioClip[] skillSwishes;
        [Range(0f, 1f)] public float normalVolume = .65f;
        [Range(0f, 1f)] public float skillVolume = .75f;

        [Header("Successful parry only")]
        public AudioClip[] parryClashes;
        [Range(0f, 1f)] public float parryVolume = .8f;

        [Header("Output")]
        public AudioMixerGroup outputMixerGroup;
        [Tooltip("0 = local player feedback; 1 = fully positional audio.")]
        [Range(0f, 1f)] public float spatialBlend;
    }
}
