using UnityEngine;
using UnityEngine.Audio;

namespace CombatSystem.Data.Audio
{
    [CreateAssetMenu(menuName = "CombatKit/Audio/Footsteps")]
    public sealed class FootstepAudioConfig : ScriptableObject
    {
        [System.Serializable]
        public sealed class Gait
        {
            public AnimationClip clip;
            [Tooltip("발이 내려와 지면에 닿는 클립 정규화 시점(0~1).")]
            [Range(0f, 1f)] public float leftContact;
            [Range(0f, 1f)] public float rightContact;
            public bool running;
        }

        public AudioClip[] leftSteps;
        public AudioClip[] rightSteps;
        public Gait[] gaits;
        [Range(0f, 1f)] public float playerWalkVolume = .32f;
        [Range(0f, 1f)] public float playerRunVolume = .45f;
        [Range(0f, 1f)] public float enemyWalkVolume = .20f;
        [Range(0f, 1f)] public float enemyRunVolume = .30f;
        [Min(.1f)] public float minDistance = 3f;
        [Min(.2f)] public float maxDistance = 18f;
        public AudioMixerGroup outputMixerGroup;

        public Gait Find(AnimationClip clip)
        {
            if (gaits != null)
                foreach (var gait in gaits)
                    if (gait != null && gait.clip == clip) return gait;
            return null;
        }
    }
}
