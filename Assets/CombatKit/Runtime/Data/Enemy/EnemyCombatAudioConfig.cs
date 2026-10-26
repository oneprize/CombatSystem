using UnityEngine;
using UnityEngine.Audio;

namespace CombatSystem.Data.Enemy
{
    [CreateAssetMenu(menuName = "CombatKit/Enemy/Combat Audio Config", fileName = "EnemyCombatAudioConfig")]
    public sealed class EnemyCombatAudioConfig : ScriptableObject
    {
        public AudioClip[] weakSwishes;
        public AudioClip[] strongSwishes;
        [Range(0f, 1f)] public float weakVolume = .45f;
        [Range(0f, 1f)] public float strongVolume = .6f;

        [Header("3D sound")]
        [Min(.1f)] public float minDistance = 5f;
        [Min(.1f)] public float maxDistance = 25f;
        public AudioMixerGroup outputMixerGroup;
    }
}
